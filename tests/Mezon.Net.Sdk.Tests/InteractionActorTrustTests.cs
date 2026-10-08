using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;
using Mezon.Net.Sdk.Entities;
using Mezon.Net.Sdk.Interactions;
using Xunit;
using ApiChannelDescription = Mezon.Net.Internal.Api.ChannelDescription;
using SdkChannel = Mezon.Net.Sdk.Entities.Channel;
using SdkUser = Mezon.Net.Sdk.Entities.User;

namespace Mezon.Net.Sdk.Tests
{
    public class InteractionActorTrustTests
    {
        [Fact]
        public async Task Button_event_marks_actor_as_client_supplied()
        {
            var router = new InteractionRouter();
            InteractionActorTrust? trust = null;
            router.OnButton("confirm", context =>
            {
                trust = ((IInteractionActor)context.Interaction).ActorTrust;
                return Task.CompletedTask;
            });

            var client = CreateClient();
            var result = await router.HandleButtonAsync(
                client,
                CreateButtonEvent("confirm"),
                CancellationToken.None).ConfigureAwait(false);

            // mezon-api MessageButtonClick forwards the client's user_id unverified.
            Assert.Equal(InteractionExecutionResult.Handled, result);
            Assert.Equal(InteractionActorTrust.ClientSupplied, trust);
        }

        [Fact]
        public async Task Dropdown_event_marks_actor_as_server_authenticated()
        {
            var router = new InteractionRouter();
            InteractionActorTrust? trust = null;
            router.OnSelect("welcome", context =>
            {
                trust = ((IInteractionActor)context.Interaction).ActorTrust;
                return Task.CompletedTask;
            });

            var client = CreateClient();
            var result = await router.HandleSelectAsync(
                client,
                CreateSelectEvent("welcome"),
                CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(InteractionExecutionResult.Handled, result);
            Assert.Equal(InteractionActorTrust.ServerAuthenticated, trust);
        }

        [Fact]
        public async Task Protected_route_rejects_client_supplied_button()
        {
            var router = new InteractionRouter();
            var invoked = false;
            router.OnButton("welcome:save", _ =>
            {
                invoked = true;
                return Task.CompletedTask;
            }).RequireServerAuthenticatedActor();

            var result = await router.HandleButtonAsync(
                CreateClient(),
                CreateButtonEvent("welcome:save"),
                CancellationToken.None);

            Assert.Equal(InteractionExecutionResult.Unauthorized, result);
            Assert.False(invoked);
        }

        [Fact]
        public async Task Collector_receives_actor_trust_per_component_kind()
        {
            var client = CreateClient();
            var collectors = new Collectors.CollectorService();
            collectors.Attach(client);

            var buttonTask = collectors.CollectComponentAsync(new Collectors.ComponentCollectorOptions
            {
                ChannelId = 20,
                ComponentId = "confirm",
            });
            await collectors.TryDispatchButtonAsync(client, CreateButtonEvent("confirm"));
            var button = await buttonTask;

            var selectTask = collectors.CollectComponentAsync(new Collectors.ComponentCollectorOptions
            {
                ChannelId = 20,
                ComponentId = "welcome",
            });
            await collectors.TryDispatchSelectAsync(client, CreateSelectEvent("welcome"));
            var select = await selectTask;

            Assert.Equal(InteractionActorTrust.ClientSupplied, ((IInteractionActor)button.Interaction!).ActorTrust);
            Assert.Equal(InteractionActorTrust.ServerAuthenticated, ((IInteractionActor)select.Interaction!).ActorTrust);
        }

        [Fact]
        public async Task Protected_route_allows_server_authenticated_dropdown()
        {
            var router = new InteractionRouter();
            router.OnSelect("welcome:save", _ => Task.CompletedTask)
                .RequireServerAuthenticatedActor();

            var result = await router.HandleSelectAsync(
                CreateClient(),
                CreateSelectEvent("welcome:save"),
                CancellationToken.None);

            Assert.Equal(InteractionExecutionResult.Handled, result);
        }

        private static MezonClient CreateClient()
        {
            var client = new MezonClient(new MezonClientOptions(1, "token"));
            var clan = new Clan(client, new ClanDesc { ClanId = 10, ClanName = "Test Clan" });
            client.Clans.Set(10, clan);
            var channel = new SdkChannel(client, new ApiChannelDescription
            {
                ClanId = 10,
                ChannelId = 20,
                ChannelLabel = "general",
                Type = 1,
            }, clan);
            client.Channels.Set(20, channel);
            client.Users.Set(40, new SdkUser(client, 40, username: "tester"));
            return client;
        }

        private static MessageButtonClickedEventData CreateButtonEvent(string buttonId)
        {
            var proto = new MessageButtonClicked
            {
                MessageId = 30,
                ChannelId = 20,
                ButtonId = buttonId,
                SenderId = 40,
                UserId = 40,
            };
            var response = (MessageButtonClickedResponse)Activator.CreateInstance(
                typeof(MessageButtonClickedResponse),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { proto },
                culture: null)!;
            return (MessageButtonClickedEventData)response;
        }

        private static DropdownBoxSelectedEventData CreateSelectEvent(string selectboxId)
        {
            var proto = new DropdownBoxSelected
            {
                MessageId = 30,
                ChannelId = 20,
                SelectboxId = selectboxId,
                SenderId = 40,
                UserId = 40,
            };
            var response = (DropdownBoxSelectedResponse)Activator.CreateInstance(
                typeof(DropdownBoxSelectedResponse),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { proto },
                culture: null)!;
            return (DropdownBoxSelectedEventData)response;
        }
    }
}
