using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NicknameSnatcher
{
    public class Options
    {
        [Option("username", Required = true,
            HelpText = "Nickname to claim.")]
        public string Username { get; set; } = default!;

        [Option("webhook", Required = true,
            HelpText = "Discord Webhook to use for messages")]
        public string Webhook { get; set; } = default!;

        [Option("proxylist", Required = false,
            HelpText = "List of HTTP proxy servers to use, in .txt format", Default = "")]
        public string ProxyList { get; set; } = default!;

        [Option("delay", Required = false, Default = 5000,
            HelpText = "Delay between attempts in milliseconds.")]
        public int Delay { get; set; }

        [Option("relog", HelpText = "Relog on program startup.", Default = false)]
        public bool Relog { get; set; }

        [Option("display_ping", HelpText = "Display latency between client and Mojang's servers.", Default = false)]
        public bool DisplayPing { get; set; }

        [Option("aggressive", HelpText = "Use aggressive variation. 'Silent' variation first checks if the nickname is available, " +
            "and thus has a lower rate limit. 'Aggressive', however, sends name change requests regardless of whether or not the nickname is available.", Default = false)]
        public bool Aggressive { get; set; }
    }
}
