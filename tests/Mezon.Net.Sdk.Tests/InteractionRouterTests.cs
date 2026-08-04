using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;
using Mezon.Net.Sdk;
using Mezon.Net.Sdk.Collectors;
using Mezon.Net.Client;
using Mezon.Net.Sdk.Entities;
using Mezon.Net.Sdk.Interactions;
using ApiChannelDescription = Mezon.Net.Internal.Api.ChannelDescription;
using SdkChannel = Mezon.Net.Sdk.Entities.Channel;
using Xunit;

namespace Mezon.Net.Sdk.Tests
{
    public class InteractionRouterTests
    {
        [Fact]
        public async Task HandleButtonAsync_matches_exact_custom_id()
        {
            var router = new InteractionRouter();
            var handledId = string.Empty;
            router.OnButton("confirm", _ =>
            {
                handledId = _.Interaction.CustomId;
                return Task.CompletedTask;
            });

            var client = CreateClient(out _);
            var result = await router.HandleButtonAsync(client, CreateButtonEvent("confirm"), CancellationToken.None)
                .ConfigureAwait(false);

            Assert.Equal(InteractionExecutionResult.Handled, result);
            Assert.Equal("confirm", handledId);
        }

        [Fact]
        public async Task HandleButtonAsync_matches_prefix_custom_id()
        {
            var router = new InteractionRouter();
            var handledId = string.Empty;
            router.OnButton("poll:*", ctx =>
            {
                handledId = ctx.Interaction.CustomId;
                return Task.CompletedTask;
            });

            var client = CreateClient(out _);
            var result = await router.HandleButtonAsync(client, CreateButtonEvent("poll:123"), CancellationToken.None)
                .ConfigureAwait(false);

            Assert.Equal(InteractionExecutionResult.Handled, result);
            Assert.Equal("poll:123", handledId);
        }

        [Fact]
        public async Task HandleButtonAsync_prefers_longest_prefix_match()
        {
            var router = new InteractionRouter();
            var handledBy = string.Empty;
            router.OnButton("poll:*", _ =>
            {
                handledBy = "poll";
                return Task.CompletedTask;
            });
            router.OnButton("poll:urgent:*", _ =>
            {
                handledBy = "poll:urgent";
                return Task.CompletedTask;
            });

            var client = CreateClient(out _);
            await router.HandleButtonAsync(client, CreateButtonEvent("poll:urgent:42"), CancellationToken.None)
                .ConfigureAwait(false);

            Assert.Equal("poll:urgent", handledBy);
        }

        [Fact]
        public async Task HandleButtonAsync_enforces_owner()
        {
            var router = new InteractionRouter();
            router.OnButton("owned", _ => Task.CompletedTask).WithOwner(999);

            var client = CreateClient(out _);
            var denied = await router.HandleButtonAsync(
                client,
                CreateButtonEvent("owned", userId: 40),
                CancellationToken.None).ConfigureAwait(false);
            var allowed = await router.HandleButtonAsync(
                client,
                CreateButtonEvent("owned", userId: 999),
                CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(InteractionExecutionResult.Unauthorized, denied);
            Assert.Equal(InteractionExecutionResult.Handled, allowed);
        }

        [Fact]
        public async Task HandleButtonAsync_one_shot_route_fires_once()
        {
            var router = new InteractionRouter();
            var runs = 0;
            router.OnButton("once", _ =>
            {
                runs++;
                return Task.CompletedTask;
            }).OneShot();

            var client = CreateClient(out _);
            var first = await router.HandleButtonAsync(client, CreateButtonEvent("once"), CancellationToken.None)
                .ConfigureAwait(false);
            var second = await router.HandleButtonAsync(client, CreateButtonEvent("once"), CancellationToken.None)
                .ConfigureAwait(false);

            Assert.Equal(InteractionExecutionResult.Handled, first);
            Assert.Equal(InteractionExecutionResult.NotHandled, second);
            Assert.Equal(1, runs);
        }

        [Fact]
        public async Task HandleButtonAsync_expired_route_is_not_invoked()
        {
            var router = new InteractionRouter
            {
                Time = TimeProvider.System,
            };
            var runs = 0;
            router.OnButton("expired", _ =>
            {
                runs++;
                return Task.CompletedTask;
            }).ExpiresAt(DateTimeOffset.UtcNow.AddSeconds(-1));

            var client = CreateClient(out _);
            var result = await router.HandleButtonAsync(client, CreateButtonEvent("expired"), CancellationToken.None)
                .ConfigureAwait(false);

            Assert.Equal(InteractionExecutionResult.Expired, result);
            Assert.Equal(0, runs);
        }

        [Fact]
        public async Task HandleButtonAsync_creates_channel_stub_without_cache_hit()
        {
            var router = new InteractionRouter();
            long? seenChannelId = null;
            router.OnButton("confirm", ctx =>
            {
                seenChannelId = ctx.Channel.Id;
                return Task.CompletedTask;
            });

            // Client has no channel 20 cached — must not call GetChannelDetail.
            var client = new MezonClient(new MezonClientOptions(1, "token"));
            client.Users.Set(40, new Entities.User(client, 40, username: "tester"));

            var result = await router.HandleButtonAsync(client, CreateButtonEvent("confirm"), CancellationToken.None)
                .ConfigureAwait(false);

            Assert.Equal(InteractionExecutionResult.Handled, result);
            Assert.Equal(20, seenChannelId);
            Assert.True(client.Channels.TryGet(20, out _));
        }

        [Fact]
        public async Task HandleSelectAsync_delivers_values()
        {
            var router = new InteractionRouter();
            IReadOnlyList<string>? values = null;
            router.OnSelect("priority", ctx =>
            {
                values = ((SelectInteraction)ctx.Interaction).Values;
                return Task.CompletedTask;
            });

            var client = CreateClient(out _);
            var result = await router.HandleSelectAsync(
                client,
                CreateSelectEvent("priority", "high", "urgent"),
                CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(InteractionExecutionResult.Handled, result);
            Assert.Equal(new[] { "high", "urgent" }, values);
        }

        private static MezonClient CreateClient(out SdkChannel channel)
        {
            var client = new MezonClient(new MezonClientOptions(1, "token"));
            var clan = new Clan(client, new ClanDesc { ClanId = 10, ClanName = "Test Clan" });
            client.Clans.Set(10, clan);
            channel = new SdkChannel(client, new ApiChannelDescription
            {
                ClanId = 10,
                ChannelId = 20,
                ChannelLabel = "general",
                Type = 1,
            }, clan);
            client.Channels.Set(20, channel);
            client.Users.Set(40, new Entities.User(client, 40, username: "tester"));
            client.Users.Set(999, new Entities.User(client, 999, username: "owner"));
            return client;
        }

        private static MessageButtonClickedEventData CreateButtonEvent(string buttonId, long userId = 40)
        {
            var proto = new MessageButtonClicked
            {
                MessageId = 30,
                ChannelId = 20,
                ButtonId = buttonId,
                SenderId = 40,
                UserId = userId,
            };
            return WrapButton(proto);
        }

        private static DropdownBoxSelectedEventData CreateSelectEvent(string selectId, params string[] values)
        {
            var proto = new DropdownBoxSelected
            {
                MessageId = 30,
                ChannelId = 20,
                SelectboxId = selectId,
                SenderId = 40,
                UserId = 40,
            };
            proto.Values.AddRange(values);
            return WrapSelect(proto);
        }

        private static MessageButtonClickedEventData WrapButton(MessageButtonClicked proto)
        {
            var response = (MessageButtonClickedResponse)Activator.CreateInstance(
                typeof(MessageButtonClickedResponse),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { proto },
                culture: null)!;
            return (MessageButtonClickedEventData)response;
        }

        private static DropdownBoxSelectedEventData WrapSelect(DropdownBoxSelected proto)
        {
            var response = (DropdownBoxSelectedResponse)Activator.CreateInstance(
                typeof(DropdownBoxSelectedResponse),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { proto },
                culture: null)!;
            return (DropdownBoxSelectedEventData)response;
        }
    }

    public class CollectorServiceTests
    {
        [Fact]
        public async Task CollectMessageAsync_collects_matching_message()
        {
            var client = CreateClient(out _);
            var collectors = new CollectorService();
            collectors.Attach(client);

            var collectTask = collectors.CollectMessageAsync(new MessageCollectorOptions
            {
                ChannelId = 20,
                UserId = 40,
            });

            await collectors.TryDispatchMessageAsync(client, CreateMessageEvent("hello")).ConfigureAwait(false);

            var result = await collectTask.ConfigureAwait(false);
            Assert.Equal(CollectorStatus.Collected, result.Status);
            Assert.NotNull(result.Message);
            Assert.Equal("hello", result.Message!.Content.Text);
        }

        [Fact]
        public async Task CollectMessageAsync_times_out()
        {
            var client = CreateClient(out _);
            var collectors = new CollectorService();
            collectors.Attach(client);

            var result = await collectors.CollectMessageAsync(new MessageCollectorOptions
            {
                ChannelId = 20,
                Timeout = TimeSpan.FromMilliseconds(50),
            }).ConfigureAwait(false);

            Assert.Equal(CollectorStatus.TimedOut, result.Status);
            Assert.Null(result.Message);
        }

        [Fact]
        public async Task CollectMessageAsync_honors_cancellation()
        {
            var client = CreateClient(out _);
            var collectors = new CollectorService();
            collectors.Attach(client);
            using var cts = new CancellationTokenSource();

            var collectTask = collectors.CollectMessageAsync(
                new MessageCollectorOptions { ChannelId = 20 },
                cts.Token);
            cts.Cancel();

            var result = await collectTask.ConfigureAwait(false);
            Assert.Equal(CollectorStatus.Cancelled, result.Status);
        }

        [Fact]
        public async Task CollectComponentAsync_collects_button_click()
        {
            var client = CreateClient(out _);
            var collectors = new CollectorService();
            collectors.Attach(client);

            var collectTask = collectors.CollectComponentAsync(new ComponentCollectorOptions
            {
                ChannelId = 20,
                ComponentId = "confirm",
            });

            await collectors.TryDispatchButtonAsync(
                client,
                CreateButtonEvent("confirm")).ConfigureAwait(false);

            var result = await collectTask.ConfigureAwait(false);
            Assert.Equal(CollectorStatus.Collected, result.Status);
            Assert.Equal("confirm", result.Interaction!.CustomId);
        }

        [Fact]
        public async Task CollectComponentAsync_race_delivers_single_result()
        {
            var client = CreateClient(out _);
            var collectors = new CollectorService();
            collectors.Attach(client);

            var collectTask = collectors.CollectComponentAsync(new ComponentCollectorOptions
            {
                ChannelId = 20,
                ComponentId = "vote",
            });

            var first = collectors.TryDispatchButtonAsync(client, CreateButtonEvent("vote", userId: 1));
            var second = collectors.TryDispatchButtonAsync(client, CreateButtonEvent("vote", userId: 2));
            await Task.WhenAll(first, second).ConfigureAwait(false);

            var result = await collectTask.ConfigureAwait(false);
            Assert.Equal(CollectorStatus.Collected, result.Status);
            Assert.NotNull(result.Interaction);
        }

        private static MezonClient CreateClient(out SdkChannel channel)
        {
            var client = new MezonClient(new MezonClientOptions(1, "token"));
            var clan = new Clan(client, new ClanDesc { ClanId = 10, ClanName = "Test Clan" });
            client.Clans.Set(10, clan);
            channel = new SdkChannel(client, new ApiChannelDescription
            {
                ClanId = 10,
                ChannelId = 20,
                ChannelLabel = "general",
                Type = 1,
            }, clan);
            client.Channels.Set(20, channel);
            client.Users.Set(40, new Entities.User(client, 40, username: "tester"));
            return client;
        }

        private static ChannelMessageEventData CreateMessageEvent(string text)
        {
            var proto = new ChannelMessage
            {
                ClanId = 10,
                ChannelId = 20,
                MessageId = 30,
                SenderId = 40,
                Content = MessageContent.CreateText(text).ToJson(),
            };
            var response = (ChannelMessageResponse)Activator.CreateInstance(
                typeof(ChannelMessageResponse),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { proto },
                culture: null)!;
            return (ChannelMessageEventData)response;
        }

        private static MessageButtonClickedEventData CreateButtonEvent(string buttonId, long userId = 40)
        {
            var proto = new MessageButtonClicked
            {
                MessageId = 30,
                ChannelId = 20,
                ButtonId = buttonId,
                SenderId = 40,
                UserId = userId,
            };
            var response = (MessageButtonClickedResponse)Activator.CreateInstance(
                typeof(MessageButtonClickedResponse),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { proto },
                culture: null)!;
            return (MessageButtonClickedEventData)response;
        }
    }
}
