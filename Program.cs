using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CommandLine;
using Microsoft.Identity.Client;
using MojangAPI;
using MojangAPI.Model;
using MojangAPI.SecurityQuestion;
using Newtonsoft.Json;
using NicknameSnatcher.Runners;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Text;
using XboxAuthNet.Game.Authenticators;
using XboxAuthNet.Game.Msal;
using static NicknameSnatcher.Logger;

namespace NicknameSnatcher
{
    internal class Program
    {
        public static Options options;
        public static string currentNickname = "";

        static async Task Main(string[] args)
        {
            await Parser.Default
            .ParseArguments<Options>(args)
            .MapResult(
                Run,
                (IEnumerable<Error> errs) => new Task<int>(() => 1));          // non-zero exit on parse failures

            return;
        }

        public static async Task<int> Run(Options opt)
        {
            options = opt;

            // check if any proxies are passed
            ProxyDelegate proxyDelegate = new ProxyDelegate([]);
            if (string.IsNullOrEmpty(opt.ProxyList))
            {
                Log($"Proxy list is empty! This is not recommended due to extremely harsh ratelimits.");
            } else
            {
                proxyDelegate = new ProxyDelegate(File.ReadAllLines(opt.ProxyList));
                Log($"Loaded a total of {proxyDelegate._proxies.Count} proxies.");
            }

            while (true)
            {
                try
                {
                    var clientHandler = new HttpClientHandler();

                    var httpClient = new HttpClient(clientHandler);

                    Mojang mojang = new Mojang(httpClient);
                    MojangAuth auth = new MojangAuth(httpClient);
                    QuestionFlow qflow = new QuestionFlow(httpClient);

                    MSession session = new MSession();
                    session = await LoginXBox(relog: opt.Relog);
                    opt.Relog = false;

                    try
                    {
                        PlayerProfile atProfile = await mojang.GetProfileUsingAccessToken(session.AccessToken);
                        currentNickname = atProfile.Name;

                        Log($"Initialized a new user! UUID: {atProfile.UUID}");
                        Log($"Username: {atProfile.Name}, skin: {atProfile.Skin.Url}");
                    } catch
                    {
                        Log($"Couldn't initialize the user. Assuming account with no name");
                        currentNickname = opt.Username;
                    }

                    if (opt.Aggressive)
                    {
                        Log($"Using aggressive variation. This will send name change requests regardless of availability.");
                        
                        await AggressiveRunner.AggressiveRunAsync(proxyDelegate, session, opt);
                    }
                    else
                    {
                        Log($"Using silent variation. This will check if the nickname is available before sending a name change request.");

                        await SilentRunner.SilentRunAsync(proxyDelegate, session, opt);
                    }
                    
                } catch (Exception e)
                {
                    Log($"[ERROR] {e}");
                    await Task.Delay(20000);
                }
            }
        }

        static async Task<MSession> LoginXBox(bool relog)
        {
            var app = MsalClientHelper.CreateDefaultApplicationBuilder("499c8d36-be2a-4231-9ebd-ef291b7bb64c").Build();
            var handlerBuilder = new JELoginHandlerBuilder();
            handlerBuilder.WithAccountManager("account.json");
            var handler = handlerBuilder.Build();
            if (relog) handler.AccountManager.ClearAccounts();

            var authenticator = handler.CreateAuthenticatorWithDefaultAccount();
            authenticator.AddMsalOAuth(app, msal => msal.DeviceCode(DeviceResultCallback));
            authenticator.AddXboxAuthForJE(xbox => xbox.Basic());
            authenticator.AddJEAuthenticator((build) =>
            {
                var collection = new AuthenticatorCollection();
                collection.AddAuthenticator(StaticValidator.Invalid, build.TokenAuthenticator());
                collection.AddAuthenticator(StaticValidator.Invalid, new FakeSessionAuthenticator(build.TokenSource, build.ProfileSource));
                return collection;
            });

            var session = await authenticator.ExecuteForLauncherAsync();
            return session;
        }

        private static async Task DeviceResultCallback(DeviceCodeResult result)
        {
            Log($"[WARNING] " + result.Message);
        }
    }
}
