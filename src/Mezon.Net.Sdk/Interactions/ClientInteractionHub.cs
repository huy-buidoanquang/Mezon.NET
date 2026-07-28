using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Mezon.Net.Models;
using Mezon.Net.Sdk.Collectors;
using Mezon.Net.Sdk.Entities;

namespace Mezon.Net.Sdk.Interactions
{
    internal sealed class ClientInteractionHub
    {
        private static readonly ConditionalWeakTable<MezonClient, ClientInteractionHub> Hubs = new();

        private readonly List<CollectorService> _collectors = new List<CollectorService>();
        private readonly List<InteractionRouter> _routers = new List<InteractionRouter>();
        private readonly object _gate = new object();
        private MezonClient? _client;
        private bool _attached;
        private Func<MessageButtonClickedEventData, Task>? _buttonHandler;
        private Func<DropdownBoxSelectedEventData, Task>? _selectHandler;
        private Func<ChannelMessageEventData, Task>? _messageHandler;

        internal static ClientInteractionHub GetOrCreate(MezonClient client)
            => Hubs.GetValue(client, static key => new ClientInteractionHub { _client = key });

        internal void RegisterCollector(CollectorService collector)
        {
            lock (_gate)
            {
                if (_collectors.Contains(collector))
                {
                    return;
                }

                _collectors.Add(collector);
                EnsureAttached();
            }
        }

        internal void UnregisterCollector(CollectorService collector)
        {
            lock (_gate)
            {
                _collectors.Remove(collector);
            }
        }

        internal void RegisterRouter(InteractionRouter router)
        {
            lock (_gate)
            {
                if (_routers.Contains(router))
                {
                    return;
                }

                _routers.Add(router);
                EnsureAttached();
            }
        }

        internal void UnregisterRouter(InteractionRouter router)
        {
            lock (_gate)
            {
                _routers.Remove(router);
            }
        }

        private void EnsureAttached()
        {
            if (_attached || _client is null)
            {
                return;
            }

            _attached = true;
            _buttonHandler = evt =>
            {
                _ = ObserveFaultAsync(OnButtonClickedAsync(evt));
                return Task.CompletedTask;
            };
            _selectHandler = evt =>
            {
                _ = ObserveFaultAsync(OnDropdownSelectedAsync(evt));
                return Task.CompletedTask;
            };
            _messageHandler = evt =>
            {
                _ = ObserveFaultAsync(OnChannelMessageAsync(evt));
                return Task.CompletedTask;
            };
            _client.MessageButtonClicked += _buttonHandler;
            _client.DropdownBoxSelected += _selectHandler;
            _client.ChannelMessageReceived += _messageHandler;
        }

        private static async Task ObserveFaultAsync(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Avoid unobserved-task crashes; handler failures are returned as Failed where applicable.
            }
        }

        private async Task OnButtonClickedAsync(MessageButtonClickedEventData eventData)
        {
            if (_client is null)
            {
                return;
            }

            CollectorService[] collectors;
            InteractionRouter[] routers;
            lock (_gate)
            {
                collectors = _collectors.ToArray();
                routers = _routers.ToArray();
            }

            for (var i = 0; i < collectors.Length; i++)
            {
                if (await collectors[i].TryDispatchButtonAsync(_client, eventData).ConfigureAwait(false))
                {
                    return;
                }
            }

            for (var i = 0; i < routers.Length; i++)
            {
                var result = await routers[i].HandleButtonAsync(_client, eventData).ConfigureAwait(false);
                if (result != InteractionExecutionResult.NotHandled)
                {
                    return;
                }
            }
        }

        private async Task OnDropdownSelectedAsync(DropdownBoxSelectedEventData eventData)
        {
            if (_client is null)
            {
                return;
            }

            CollectorService[] collectors;
            InteractionRouter[] routers;
            lock (_gate)
            {
                collectors = _collectors.ToArray();
                routers = _routers.ToArray();
            }

            for (var i = 0; i < collectors.Length; i++)
            {
                if (await collectors[i].TryDispatchSelectAsync(_client, eventData).ConfigureAwait(false))
                {
                    return;
                }
            }

            for (var i = 0; i < routers.Length; i++)
            {
                var result = await routers[i].HandleSelectAsync(_client, eventData).ConfigureAwait(false);
                if (result != InteractionExecutionResult.NotHandled)
                {
                    return;
                }
            }
        }

        private async Task OnChannelMessageAsync(ChannelMessageEventData eventData)
        {
            if (_client is null)
            {
                return;
            }

            CollectorService[] collectors;
            lock (_gate)
            {
                if (_collectors.Count == 0)
                {
                    return;
                }

                collectors = _collectors.ToArray();
            }

            for (var i = 0; i < collectors.Length; i++)
            {
                if (await collectors[i].TryDispatchMessageAsync(_client, eventData).ConfigureAwait(false))
                {
                    return;
                }
            }
        }
    }
}
