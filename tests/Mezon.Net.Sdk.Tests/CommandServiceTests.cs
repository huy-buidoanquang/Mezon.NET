using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Internal.Api;
using Mezon.Net.Models;
using Mezon.Net.Sdk;
using Mezon.Net.Sdk.Commands;
using Mezon.Net.Client;
using Mezon.Net.Sdk.Entities;
using ApiChannelDescription = Mezon.Net.Internal.Api.ChannelDescription;
using SdkChannel = Mezon.Net.Sdk.Entities.Channel;
using Xunit;

namespace Mezon.Net.Sdk.Tests
{
    public class CommandParserTests
    {
        [Fact]
        public void TryParse_splits_quoted_arguments()
        {
            var parser = new CommandParser();
            Assert.True(parser.TryParse("!say \"hello world\" there", "!", out var parsed));
            Assert.Equal("say", parsed.Name);
            Assert.Equal(new[] { "hello world", "there" }, parsed.Args);
        }

        [Fact]
        public void TryParse_supports_single_quoted_arguments()
        {
            var parser = new CommandParser();
            Assert.True(parser.TryParse("!echo 'don\\'t stop'", "!", out var parsed));
            Assert.Equal("echo", parsed.Name);
            Assert.Equal(new[] { "don't stop" }, parsed.Args);
        }

        [Fact]
        public void TryParse_normalizes_name_when_ignore_case()
        {
            var parser = new CommandParser(CommandCasePolicy.IgnoreCase);
            Assert.True(parser.TryParse("!Ping", "!", out var parsed));
            Assert.Equal("ping", parsed.Name);
        }

        [Fact]
        public void TryParse_preserves_name_when_case_sensitive()
        {
            var parser = new CommandParser(CommandCasePolicy.CaseSensitive);
            Assert.True(parser.TryParse("!Ping", "!", out var parsed));
            Assert.Equal("Ping", parsed.Name);
        }

        [Fact]
        public void TryParse_returns_false_without_prefix()
        {
            var parser = new CommandParser();
            Assert.False(parser.TryParse("ping", "!", out _));
        }

        [Fact]
        public void TryParse_returns_false_for_prefix_only()
        {
            var parser = new CommandParser();
            Assert.False(parser.TryParse("!", "!", out _));
        }
    }

    public class CommandServiceTests
    {
        [Fact]
        public void AddCommand_registers_aliases()
        {
            var service = new CommandService("!");
            service.AddCommand("ping", _ => Task.CompletedTask).WithAlias("pong", "latency");

            Assert.Single(service.Commands);
            Assert.Equal("ping", service.Commands.First().Name);
        }

        [Fact]
        public async Task HandleMessageAsync_resolves_aliases()
        {
            var service = new CommandService("!");
            var executed = false;
            service.AddCommand("ping", _ =>
            {
                executed = true;
                return Task.CompletedTask;
            }).WithAlias("pong");

            var client = CreateClient(out _);
            var result = await service.HandleMessageAsync(
                client,
                CreateMessage("!pong"),
                CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(CommandExecutionResult.Executed, result);
            Assert.True(executed);
        }

        [Fact]
        public async Task HandleMessageAsync_honors_case_sensitive_policy()
        {
            var service = new CommandService("!", CommandCasePolicy.CaseSensitive);
            service.AddCommand("Ping", _ => Task.CompletedTask);

            var client = CreateClient(out _);
            var miss = await service.HandleMessageAsync(client, CreateMessage("!ping"), CancellationToken.None).ConfigureAwait(false);
            var hit = await service.HandleMessageAsync(client, CreateMessage("!Ping"), CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(CommandExecutionResult.UnknownCommand, miss);
            Assert.Equal(CommandExecutionResult.Executed, hit);
        }

        [Fact]
        public async Task HandleMessageAsync_enforces_cooldown()
        {
            var service = new CommandService("!");
            var runs = 0;
            service.AddCommand("ping", _ =>
            {
                runs++;
                return Task.CompletedTask;
            }).WithCooldown(TimeSpan.FromSeconds(30));

            var client = CreateClient(out _);
            var first = await service.HandleMessageAsync(client, CreateMessage("!ping"), CancellationToken.None).ConfigureAwait(false);
            var second = await service.HandleMessageAsync(client, CreateMessage("!ping"), CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(CommandExecutionResult.Executed, first);
            Assert.Equal(CommandExecutionResult.OnCooldown, second);
            Assert.Equal(1, runs);
        }

        [Fact]
        public async Task HandleMessageAsync_runs_middleware_in_registration_order()
        {
            var service = new CommandService("!");
            var order = new List<string>();

            service.Use(async (ctx, next) =>
            {
                order.Add("mw1-before");
                await next(ctx).ConfigureAwait(false);
                order.Add("mw1-after");
            });
            service.Use(async (ctx, next) =>
            {
                order.Add("mw2-before");
                await next(ctx).ConfigureAwait(false);
                order.Add("mw2-after");
            });
            service.AddCommand("ping", _ =>
            {
                order.Add("handler");
                return Task.CompletedTask;
            });

            var client = CreateClient(out _);
            await service.HandleMessageAsync(client, CreateMessage("!ping"), CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(new[] { "mw1-before", "mw2-before", "handler", "mw2-after", "mw1-after" }, order);
        }

        [Fact]
        public async Task Attach_subscriber_returns_immediately_and_runs_command()
        {
            var service = new CommandService("!");
            var executed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            service.AddCommand("ping", _ =>
            {
                executed.TrySetResult(true);
                return Task.CompletedTask;
            });

            var client = CreateClient(out _);
            service.Attach(client);

            var handlerField = typeof(CommandService).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var handler = (Func<ChannelMessageEventData, Task>)handlerField.GetValue(service)!;
            var subscribeTask = handler(CreateMessage("!ping"));
            var completed = await Task.WhenAny(subscribeTask, Task.Delay(200)).ConfigureAwait(false);
            Assert.Same(subscribeTask, completed);
            await subscribeTask.ConfigureAwait(false);

            var ran = await Task.WhenAny(executed.Task, Task.Delay(2000)).ConfigureAwait(false);
            Assert.Same(executed.Task, ran);
            Assert.True(await executed.Task.ConfigureAwait(false));
        }

        [Fact]
        public async Task HandleMessageAsync_blocks_unauthorized_commands()
        {
            var service = new CommandService("!");
            service.AddCommand("admin", _ => Task.CompletedTask)
                .Require(ctx => ctx.Author.Id == 999);

            var client = CreateClient(out _);
            var result = await service.HandleMessageAsync(client, CreateMessage("!admin"), CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(CommandExecutionResult.Unauthorized, result);
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

        private static ChannelMessageEventData CreateMessage(string text)
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
    }
}
