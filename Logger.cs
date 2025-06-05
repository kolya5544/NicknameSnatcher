using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicknameSnatcher
{
    public static class Logger
    {
        private static readonly object _fileLock = new();
        private static readonly List<(DateTime ts, string msg)> _buffer = new(); // rolling buffer
        private static readonly SemaphoreSlim _bufferLock = new(1, 1);           // async-friendly lock
        private static readonly TimeSpan _flushInterval = TimeSpan.FromMilliseconds(7000);
        private static readonly Timer _timer = new(OnTimer, null, _flushInterval, _flushInterval);
        private static readonly HttpClient _client = new();                      // reuse!

        // --- public entry point -------------------------------------------------
        public static void Log(string msg = "")
        {
            // 1) write to disk & console immediately (unchanged)
            lock (_fileLock)
            {
                var line = $"[{DateTime.UtcNow:HH:mm:ss}] {msg}\r\n";
                File.AppendAllText("log.log", line);
                Console.Write(line);
            }

            // 2) stage for batch-send
            _ = StageAsync(msg);
        }

        // --- private helpers ----------------------------------------------------
        private static async Task StageAsync(string msg)
        {
            await _bufferLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _buffer.Add((DateTime.UtcNow, msg));
            }
            finally
            {
                _bufferLock.Release();
            }
        }

        // fires once, 2 s after the most-recent log
        private static async void OnTimer(object? _)
        {
            List<(DateTime ts, string msg)> snapshot;

            await _bufferLock.WaitAsync().ConfigureAwait(false);
            try
            {
                snapshot = new(_buffer);   // copy
                _buffer.Clear();           // reset buffer
            }
            finally
            {
                _bufferLock.Release();
            }

            if (snapshot.Count == 0) return; // nothing to send

            // build a Discord embed
            var sb = new StringBuilder();
            foreach (var (ts, msg) in snapshot)
                sb.AppendLine($"[{ts:HH:mm:ss}] {msg}");

            dynamic payload = new ExpandoObject();
            payload.username = "NicknameSnatcher";
            payload.avatar_url = $"https://mc-heads.net/avatar/{Program.currentNickname}";
            payload.content = "";           // leave empty when using embed(s)
            bool everyone = false;

            // determine color based on message content
            int color = 0x2ECC71; // default to green
            if (sb.ToString().Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                color = 0xE74C3C; // red for errors
                everyone = true;
            }
            else if (sb.ToString().Contains("warning", StringComparison.OrdinalIgnoreCase))
            {
                color = 0xF1C40F; // yellow for warnings
                everyone = true;
            }

            // determine title suffix if there are errors
            string suffix = "";
            if (sb.ToString().Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                suffix = " - Errors Detected";
                everyone = true;
            }
            else if (sb.ToString().Contains("warning", StringComparison.OrdinalIgnoreCase))
            {
                suffix = " - Warnings Detected";
                everyone = true;
            }
            if (everyone)
            {
                payload.content = "@everyone"; // notify everyone
            }

            payload.embeds = new[]
            {
                new
                {
                    title       = $"NicknameSnatcher - `{Program.currentNickname}`{suffix}",
                    description = $"```{sb}```",   // put logs in a code-block
                    color       = color        
                }
            };

            try
            {
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _client.PostAsync(Program.options.Webhook, content).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send log batch to Discord: {ex.Message}");
            }
        }
    }
}
