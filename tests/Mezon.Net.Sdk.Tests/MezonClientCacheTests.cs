using System;
using System.Reflection;
using System.Threading.Tasks;
using Mezon.Net.Internal.Api;
using Mezon.Net.Internal.Realtime;
using Mezon.Net.Models;
using Mezon.Net.Sdk;
using Mezon.Net.Sdk.Entities;
using ApiChannelDescription = Mezon.Net.Internal.Api.ChannelDescription;
using Xunit;

namespace Mezon.Net.Sdk.Tests
{
    public sealed class MezonClientCacheTests
    {
        [Fact]
        public void Message_update_mutates_cached_message_content_in_place()
        {
            var (_, channel) = CreateFixture(clanId: 1, channelId: 10);
            var message = SeedMessage(channel, messageId: 100, content: "hello");

            message.UpdateFrom(CreateUpdateResponse(new ChannelMessageUpdate
            {
                ClanId = 1,
                ChannelId = 10,
                MessageId = 100,
                Content = "updated",
            }));

            Assert.Same(message, channel.Messages.Get(100));
            Assert.Equal("updated", message.Source!.Value.Content);
        }

        [Fact]
        public async Task ChannelMessageRemoved_removes_message_from_channel_cache()
        {
            var (client, channel) = CreateFixture(clanId: 1, channelId: 10);
            SeedMessage(channel, messageId: 100, content: "hello");
            BindCacheListeners(client);

            var removeEvent = (ChannelMessageRemoveEventData)CreateRemoveResponse(new ChannelMessageRemove
            {
                ClanId = 1,
                ChannelId = 10,
                MessageId = 100,
            });

            await InvokeCacheHandlerAsync(client, "OnChannelMessageRemovedInternalAsync", removeEvent);

            Assert.Null(channel.Messages.Get(100));
        }

        [Fact]
        public void Clan_Channels_does_not_return_unrelated_clan_channels()
        {
            var client = new MezonClient(new MezonClientOptions { BotId = 1, Token = "token" });
            var clan1 = new Clan(client, new ClanDesc { ClanId = 1, ClanName = "Clan One" });
            var clan2 = new Clan(client, new ClanDesc { ClanId = 2, ClanName = "Clan Two" });
            client.Clans.Set(1, clan1);
            client.Clans.Set(2, clan2);

            var channelInClan1 = new TextChannel(client, new ApiChannelDescription
            {
                ClanId = 1,
                ChannelId = 10,
                ChannelLabel = "general",
                Type = 1,
            }, clan1);
            var channelInClan2 = new TextChannel(client, new ApiChannelDescription
            {
                ClanId = 2,
                ChannelId = 20,
                ChannelLabel = "general",
                Type = 1,
            }, clan2);
            client.Channels.Set(10, channelInClan1);
            client.Channels.Set(20, channelInClan2);

            Assert.Same(channelInClan1, clan1.Channels.Get(10));
            Assert.Null(clan1.Channels.Get(20));
            Assert.Same(channelInClan2, clan2.Channels.Get(20));
            Assert.Null(clan2.Channels.Get(10));
        }

        [Fact]
        public async Task ChannelMessageReceived_reuses_cached_message_instance()
        {
            var (client, channel) = CreateFixture(clanId: 1, channelId: 10);
            var message = SeedMessage(channel, messageId: 100, content: "hello");
            BindCacheListeners(client);

            var receiveEvent = (ChannelMessageEventData)CreateMessageResponse(new ChannelMessage
            {
                ClanId = 1,
                ChannelId = 10,
                MessageId = 100,
                Content = "updated via receive",
            });

            await InvokeCacheHandlerAsync(client, "OnChannelMessageInternalAsync", receiveEvent);

            Assert.Same(message, channel.Messages.Get(100));
            Assert.Equal("updated via receive", message.Source!.Value.Content);
        }

        private static (MezonClient Client, TextChannel Channel) CreateFixture(long clanId, long channelId)
        {
            var client = new MezonClient(new MezonClientOptions { BotId = 1, Token = "token" });
            var clan = new Clan(client, new ClanDesc { ClanId = clanId, ClanName = "Test Clan" });
            client.Clans.Set(clanId, clan);

            var channel = new TextChannel(client, new ApiChannelDescription
            {
                ClanId = clanId,
                ChannelId = channelId,
                ChannelLabel = "general",
                Type = 1,
            }, clan);
            client.Channels.Set(channelId, channel);
            return (client, channel);
        }

        private static Message SeedMessage(TextChannel channel, long messageId, string content)
        {
            var message = new Message(
                channel.Clan.GetClient(),
                channel,
                CreateMessageResponse(new ChannelMessage
                {
                    ClanId = channel.ClanId,
                    ChannelId = channel.Id,
                    MessageId = messageId,
                    Content = content,
                }));
            channel.Messages.Set(messageId, message);
            return message;
        }

        [Fact]
        public async Task MessageReactionReceived_updates_cached_message_reactions()
        {
            var (client, channel) = CreateFixture(clanId: 1, channelId: 10);
            var message = SeedMessage(channel, messageId: 100, content: "hello");
            BindCacheListeners(client);

            var reactionEvent = (MessageReactionEventData)CreateReactionResponse(new MessageReaction
            {
                ClanId = 1,
                ChannelId = 10,
                MessageId = 100,
                EmojiId = 7,
                Emoji = ":smile:",
                Count = 1,
                SenderId = 42,
            });

            await InvokeCacheHandlerAsync(client, "OnMessageReactionInternalAsync", reactionEvent);

            Assert.Equal(1, message.Source!.Value.Reactions.Count);
            Assert.Equal(7, message.Source.Value.Reactions[0].EmojiId);
        }

        private static MessageReactionResponse CreateReactionResponse(MessageReaction proto)
            => InvokeInternalFactory<MessageReactionResponse>(typeof(MessageReactionResponse), proto);

        [Fact]
        public async Task ChannelUpdated_thread_join_returns_without_awaiting_socket()
        {
            var (client, _) = CreateFixture(clanId: 1, channelId: 10);
            BindCacheListeners(client);

            var updateEvent = (ChannelUpdatedEventEventData)CreateChannelUpdatedResponse(new ChannelUpdatedEvent
            {
                ClanId = 1,
                ChannelId = 99,
                ChannelLabel = "thread",
                ChannelType = 7,
                ChannelPrivate = false,
                Status = 1,
            });

            var handler = typeof(MezonClient).GetMethod("OnChannelUpdatedInternalAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var task = (Task)handler.Invoke(client, new object[] { updateEvent })!;

            // Must complete without waiting for JoinChannelChat (no connected socket).
            var completed = await Task.WhenAny(task, Task.Delay(500)).ConfigureAwait(false);
            Assert.Same(task, completed);
            await task.ConfigureAwait(false);
        }

        [Fact]
        public async Task UserChannelAdded_join_returns_without_awaiting_socket()
        {
            var (client, _) = CreateFixture(clanId: 1, channelId: 10);
            BindCacheListeners(client);

            var added = new UserChannelAdded
            {
                ClanId = 1,
            };
            added.ChannelDesc = new global::Mezon.Net.Internal.Api.ChannelDescription
            {
                ChannelId = 55,
                Type = 1,
                ChannelPrivate = 0,
            };
            added.Users.Add(new UserProfileRedis { UserId = 1 });

            var addedEvent = (UserChannelAddedEventData)CreateUserChannelAddedResponse(added);
            var handler = typeof(MezonClient).GetMethod("OnUserChannelAddedInternalAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var task = (Task)handler.Invoke(client, new object[] { addedEvent })!;

            var completed = await Task.WhenAny(task, Task.Delay(500)).ConfigureAwait(false);
            Assert.Same(task, completed);
            await task.ConfigureAwait(false);
        }

        private static ChannelUpdatedEventResponse CreateChannelUpdatedResponse(ChannelUpdatedEvent proto)
            => InvokeInternalFactory<ChannelUpdatedEventResponse>(typeof(ChannelUpdatedEventResponse), proto);

        private static UserChannelAddedResponse CreateUserChannelAddedResponse(UserChannelAdded proto)
            => InvokeInternalFactory<UserChannelAddedResponse>(typeof(UserChannelAddedResponse), proto);

        private static ChannelMessageResponse CreateMessageResponse(ChannelMessage proto)
            => InvokeInternalFactory<ChannelMessageResponse>(typeof(ChannelMessageResponse), proto);

        private static ChannelMessageUpdateResponse CreateUpdateResponse(ChannelMessageUpdate proto)
            => InvokeInternalFactory<ChannelMessageUpdateResponse>(typeof(ChannelMessageUpdateResponse), proto);

        private static ChannelMessageRemoveResponse CreateRemoveResponse(ChannelMessageRemove proto)
            => InvokeInternalFactory<ChannelMessageRemoveResponse>(typeof(ChannelMessageRemoveResponse), proto);

        private static TResponse InvokeInternalFactory<TResponse>(Type responseType, object proto)
        {
            return (TResponse)Activator.CreateInstance(
                responseType,
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new[] { proto },
                culture: null)!;
        }

        private static void BindCacheListeners(MezonClient client)
        {
            var method = typeof(MezonClient).GetMethod("BindCacheListeners", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(MezonClient).FullName, "BindCacheListeners");
            method.Invoke(client, null);
        }

        private static Task InvokeCacheHandlerAsync(MezonClient client, string methodName, object eventData)
        {
            var method = typeof(MezonClient).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(MezonClient).FullName, methodName);
            return (Task)method.Invoke(client, new[] { eventData })!;
        }
    }
}
