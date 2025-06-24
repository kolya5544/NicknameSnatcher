using MojangAPI.Model;
using MojangAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core.Auth;
using static NicknameSnatcher.Program;
using static NicknameSnatcher.Logger;
using MojangAPI.SecurityQuestion;

namespace NicknameSnatcher.Runners
{
    internal class SilentRunner
    {
        public static Random rng = new Random();

        public static async Task SilentRunAsync(ProxyDelegate proxyDel, MSession session, Options opt)
        {
            while (true)
            {
                // occasionally make sure we're still authed
                switch (rng.Next(0, 5000))
                {
                    case 0:
                        try
                        {
                            var clientHandler = new HttpClientHandler();

                            var httpClient = new HttpClient(clientHandler);

                            Mojang mojang = new Mojang(httpClient);
                            MojangAuth auth = new MojangAuth(httpClient);
                            QuestionFlow qflow = new QuestionFlow(httpClient);

                            PlayerProfile atProfile = await mojang.GetProfileUsingAccessToken(session.AccessToken);
                        }
                        catch (Exception ex)
                        {
                            if (ex.ToString().Contains("403: Forbid")) throw ex;
                        }
                        break;
                    default:
                        break;
                }

                // 1) check availability
                bool availability = false;
                try
                {
                    var check = await proxyDel.RunAsync(async http =>
                    {
                        // Your Mojang wrapper should allow injecting an HttpClient
                        var api = new Mojang(http);
                        return await api.GetUUID(opt.Username);
                    });

                    if (!string.IsNullOrEmpty(check.UUID)) availability = false;
                    else availability = true;
                } catch (Exception e)
                {
                    if (e.ToString().Contains("Couldn't find any profile with name"))
                    {
                        availability = true;
                    }
                    else
                    {
                        Log($"[ERROR] Exception while checking nickname availability: {e}");
                        throw e;  // rethrow to handle it upstream
                    }
                }

                Log($"Checking nickname {opt.Username}: {(availability ? "AVAILABLE!" : "Not available")}");

                // 2) try to change, if appropriate
                if (availability)
                {
                    Log($"[WARNING] Nickname {opt.Username} appears to be available! Attempting to claim it...");

                    var result = await proxyDel.RunAsync(async http =>
                    {
                        var api = new Mojang(http);
                        return await api.ChangeName(session.AccessToken, opt.Username);
                    });

                    if (result.Name == opt.Username)
                    {
                        Log($"Successfully changed nickname to {opt.Username}!");
                        currentNickname = opt.Username;
                    }
                    else
                    {
                        Log("[ERROR] Failed to change nickname");
                    }
                }

                await Task.Delay(opt.Delay);
            }
        }
    }
}
