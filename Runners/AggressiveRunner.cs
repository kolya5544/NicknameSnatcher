using CmlLib.Core.Auth;
using MojangAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NicknameSnatcher.Program;
using static NicknameSnatcher.Logger;

namespace NicknameSnatcher.Runners
{
    public class AggressiveRunner
    {
        public static async Task AggressiveRunAsync(ProxyDelegate proxyDel, MSession session, Options opt)
        {
            while (true)
            {
                // blindly attempt the nickname change
                try
                {
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
                } catch (Exception e)
                {
                    if (!e.ToString().Contains("Could not change name for profile"))
                    {
                        Log(e.ToString());
                        throw e;
                    }
                }

                await Task.Delay(opt.Delay);
            }
        }
    }
}
