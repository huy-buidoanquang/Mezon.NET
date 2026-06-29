using System;
using Mezon.NET.Abstractions;
using Mezon.NET.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Mezon.NET
{
    internal class WebSocketAdapterFactory : IWebSocketAdapterFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public WebSocketAdapterFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IWebSocketAdapter Create(WebSocketAdapterEnum adapterType)
        {
            return adapterType switch
            {
                WebSocketAdapterEnum.Text => _serviceProvider.GetRequiredService<WebSocketAdapterText>(),
                WebSocketAdapterEnum.Protobuf => _serviceProvider.GetRequiredService<WebSocketAdapterProtobuf>(),
                _ => _serviceProvider.GetRequiredService<WebSocketAdapterText>()
            };
        }
    }
}
