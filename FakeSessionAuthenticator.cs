using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.Sessions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XboxAuthNet.Game.Authenticators;
using XboxAuthNet.Game.SessionStorages;

namespace NicknameSnatcher
{
    public class FakeSessionAuthenticator : SessionAuthenticator<JEProfile>
    {
        private readonly ISessionSource<JEToken> _jeSessionSource;

        public FakeSessionAuthenticator(
            ISessionSource<JEToken> jeSessionSource,
            ISessionSource<JEProfile> sessionSource)
            : base(sessionSource) =>
            _jeSessionSource = jeSessionSource;

        protected override ValueTask<JEProfile?> Authenticate(AuthenticateContext context)
        {
            var token = _jeSessionSource.Get(context.SessionStorage);
            if (string.IsNullOrEmpty(token?.AccessToken))
                throw new JEAuthException("JEToken.AccessToken was empty. JETokenAuthenticator must run first.");

            return new ValueTask<JEProfile?>(new JEProfile());
        }
    }
}
