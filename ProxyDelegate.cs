using MojangAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NicknameSnatcher
{
    public sealed class ProxyDelegate : IAsyncDisposable
    {
        public readonly IReadOnlyList<string> _proxies;
        private int _index = -1;                   // round-robin cursor
        private HttpClient? _current;               // reused until 429
        private readonly SemaphoreSlim _mutex = new(1, 1);
        private Stopwatch _stopwatch = new Stopwatch();

        public ProxyDelegate(IEnumerable<string>? proxies)
        {
            var arr = (proxies == null || !proxies.Any())
                      ? new[] { (string?)null }          // fallback: direct connect
                      : proxies.ToArray();

            // Fisher-Yates shuffle so each start picks a different order
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);       // .NET 6+: thread-safe RNG
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }

            _proxies = arr;
        }


        /// <summary>
        /// Executes <paramref name="work"/> and transparently switches to the
        /// next proxy whenever a Mojang 429 (rate limit) is thrown.
        /// </summary>
        public async Task<T> RunAsync<T>(
            Func<HttpClient, Task<T>> work,
            int maxRotations = 5,          // safety-valve
            CancellationToken cancel = default)
        {
            if (work is null) throw new ArgumentNullException(nameof(work));

            var rotations = 0;

            while (true)
            {
                cancel.ThrowIfCancellationRequested();
                HttpClient client = await GetOrCreateClientAsync(cancel);

                _stopwatch.Start();
                try
                {
                    var r = await work(client);
                    _stopwatch.Stop();
                    if (Program.options.DisplayPing)
                        Logger.Log($"Proxy: {_proxies[_index]} | Ping: {_stopwatch.ElapsedMilliseconds} ms");
                    _stopwatch.Reset();
                    return r;
                }
                catch (MojangException ex) when (ex.StatusCode == 429)
                {
                    Logger.Log($"Hit 429! Rotating proxies...");

                    // Rotate and retry
                    rotations++;
                    if (rotations > maxRotations)
                        throw new InvalidOperationException("All proxies were rate-limited.", ex);

                    await RotateAsync(cancel);
                    await Task.Delay(TimeSpan.FromSeconds(1), cancel);
                }
                catch (TaskCanceledException ex) when
               (!cancel.IsCancellationRequested && 
                (ex.InnerException is TimeoutException 
                 || ex.Message.Contains("HttpClient.Timeout",
                                        StringComparison.OrdinalIgnoreCase)))
                {
                    rotations++;
                    if (rotations > maxRotations)
                        throw new TimeoutException("All proxies have timed out.", ex);

                    await RotateAsync(cancel); 
                    await Task.Delay(500, cancel);
                }
            }
        }

        /* ---------- plumbing ---------- */

        private async Task<HttpClient> GetOrCreateClientAsync(CancellationToken ct)
        {
            await _mutex.WaitAsync(ct);
            try
            {
                if (_current == null)
                {
                    _index = (_index + 1) % _proxies.Count;
                    _current = BuildClient(_proxies[_index]);
                }
                return _current;
            }
            finally
            {
                _mutex.Release();
            }
        }

        private async Task RotateAsync(CancellationToken ct)
        {
            await _mutex.WaitAsync(ct);
            try
            {
                _current?.Dispose();
                _current = null;                    // recreated on next call
            }
            finally
            {
                _mutex.Release();
            }
        }

        private static HttpClient BuildClient(string? proxyAddress)
        {
            var handler = new HttpClientHandler();
            if (proxyAddress != null)
            {
                Logger.Log($"Using IP: {proxyAddress}");
                handler.Proxy = new WebProxy(proxyAddress);
                handler.UseProxy = true;
            }
            return new HttpClient(handler, disposeHandler: true)
            { Timeout = TimeSpan.FromSeconds(10) };
        }

        public async ValueTask DisposeAsync()
        {
            _current?.Dispose();
            _mutex.Dispose();
            await Task.CompletedTask;
        }
    }
}
