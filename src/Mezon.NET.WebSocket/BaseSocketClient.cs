using Mezon.NET.Api;
using Mezon.NET.Api.Abstractions;
using Mezon.NET.Core;
using Mezon.NET.Core.Abstractions;

namespace Mezon.NET.WebSocket
{
    public abstract partial class BaseSocketClient : BaseMezonClient, IMezonClient, IApiClientProvider
    {
        public BaseSocketClient(MezonConfiguration mezonConfiguration) : base(mezonConfiguration)
        {
        }

        public IMezonApiClient MezonApiClient => throw new System.NotImplementedException();
    }
}
