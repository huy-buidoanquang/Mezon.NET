using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Models;
using Mezon.Net.Client;
using Mezon.Net.Sdk.Entities;

namespace Mezon.Net.Sdk.Commands
{
    public sealed class CommandService
    {
        private readonly Dictionary<string, CommandInfo> _lookup = new Dictionary<string, CommandInfo>(StringComparer.Ordinal);
        private readonly List<CommandInfo> _commands = new List<CommandInfo>();
        private readonly List<CommandMiddleware> _middleware = new List<CommandMiddleware>();
        private readonly CommandParser _parser;
        private MezonClient? _client;
        private Func<ChannelMessageEventData, Task>? _handler;

        public CommandService(string prefix, CommandCasePolicy casePolicy = CommandCasePolicy.IgnoreCase)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                throw new ArgumentException("Command prefix is required.", nameof(prefix));
            }

            Prefix = prefix;
            _parser = new CommandParser(casePolicy);
            CooldownStore = new InMemoryCommandCooldownStore();
        }

        public string Prefix { get; set; }
        public CommandCasePolicy CasePolicy
        {
            get => _parser.CasePolicy;
            set => _parser.CasePolicy = value;
        }

        public bool IgnoreBots { get; set; } = true;
        public long? ChannelFilter { get; set; }
        public ICommandCooldownStore CooldownStore { get; set; }

        public IReadOnlyCollection<CommandInfo> Commands => _commands;

        public CommandRegistration AddCommand(string name, CommandHandler handler)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Command name is required.", nameof(name));
            }

            var normalized = _parser.NormalizeName(name.Trim());
            if (_lookup.ContainsKey(normalized))
            {
                throw new InvalidOperationException($"Command '{name}' is already registered.");
            }

            var info = new CommandInfo(normalized, handler);
            _lookup[normalized] = info;
            _commands.Add(info);
            return new CommandRegistration(this, info);
        }

        public CommandService Use(CommandMiddleware middleware)
        {
            if (middleware is null)
            {
                throw new ArgumentNullException(nameof(middleware));
            }

            _middleware.Add(middleware);
            return this;
        }

        public MezonClient Attach(MezonClient client)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (_client is not null)
            {
                throw new InvalidOperationException("CommandService is already attached to a client.");
            }

            _client = client;
            _handler = evt => HandleMessageAsync(client, evt);
            client.ChannelMessageReceived += _handler;
            return client;
        }

        public void Detach()
        {
            if (_client is null || _handler is null)
            {
                return;
            }

            _client.ChannelMessageReceived -= _handler;
            _client = null;
            _handler = null;
        }

        internal void AddAliases(CommandInfo command, params string[] aliases)
        {
            if (aliases is null || aliases.Length == 0)
            {
                return;
            }

            var normalizedAliases = new List<string>(command.Aliases.Count + aliases.Length);
            normalizedAliases.AddRange(command.Aliases);
            for (var i = 0; i < aliases.Length; i++)
            {
                var alias = aliases[i];
                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                var normalized = _parser.NormalizeName(alias.Trim());
                if (_lookup.TryGetValue(normalized, out var existing) && !ReferenceEquals(existing, command))
                {
                    throw new InvalidOperationException($"Alias '{alias}' is already registered.");
                }

                _lookup[normalized] = command;
                normalizedAliases.Add(normalized);
            }

            command.Aliases = normalizedAliases;
        }

        public async Task<CommandExecutionResult> HandleMessageAsync(
            MezonClient client,
            ChannelMessageEventData messageEvent,
            CancellationToken cancellationToken = default)
        {
            var message = (ChannelMessageResponse)messageEvent;

            if (IgnoreBots && message.SenderId == client.BotId)
            {
                return CommandExecutionResult.NotACommand;
            }

            if (ChannelFilter is long allowedChannelId && message.ChannelId != allowedChannelId)
            {
                return CommandExecutionResult.NotACommand;
            }

            var text = ExtractPlainText(message.Content);
            if (!_parser.TryParse(text, Prefix, out var parsed))
            {
                return CommandExecutionResult.NotACommand;
            }

            if (!_lookup.TryGetValue(parsed.Name, out var command))
            {
                return CommandExecutionResult.UnknownCommand;
            }

            var context = await CreateContextAsync(client, message, command.Name, parsed.Args, cancellationToken).ConfigureAwait(false);

            if (command.Cooldown is TimeSpan cooldown
                && !CooldownStore.TryBegin(command.Name, context.Author.Id, cooldown, out _))
            {
                return CommandExecutionResult.OnCooldown;
            }

            if (!await command.IsAuthorizedAsync(context).ConfigureAwait(false))
            {
                return CommandExecutionResult.Unauthorized;
            }

            try
            {
                var pipeline = BuildPipeline(command.Handler);
                await pipeline(context).ConfigureAwait(false);
                return CommandExecutionResult.Executed;
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                try
                {
                    await context.ReplyTextAsync("Command error: " + ex.Message).ConfigureAwait(false);
                }
                catch
                {
                    // ignore notify failures
                }

                return CommandExecutionResult.Failed;
            }
        }

        private CommandMiddlewareDelegate BuildPipeline(CommandHandler handler)
        {
            CommandMiddlewareDelegate pipeline = context => handler(context);
            for (var i = _middleware.Count - 1; i >= 0; i--)
            {
                var middleware = _middleware[i];
                var next = pipeline;
                pipeline = context => middleware(context, next);
            }

            return pipeline;
        }

        private async Task<ICommandContext> CreateContextAsync(
            MezonClient client,
            ChannelMessageResponse message,
            string commandName,
            IReadOnlyList<string> args,
            CancellationToken cancellationToken)
        {
            var channel = await client.GetOrCreateChannelFromMessageAsync(message, cancellationToken).ConfigureAwait(false);
            if (!channel.Messages.TryGet(message.MessageId, out var messageEntity))
            {
                messageEntity = new Message(client, channel, message);
                channel.Messages.Set(message.MessageId, messageEntity);
            }

            var author = await client.GetUserAsync(message.SenderId, cancellationToken).ConfigureAwait(false);
            Clan? clan = null;
            if (message.ClanId != 0)
            {
                if (client.Clans.TryGet(message.ClanId, out var cachedClan))
                {
                    clan = cachedClan;
                }
                else
                {
                    try
                    {
                        clan = await client.GetClanAsync(message.ClanId, cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Commands can still run using Channel.ClanId when clan cache seed failed.
                    }
                }
            }

            return new CommandContext(
                client,
                messageEntity,
                channel,
                clan,
                author,
                Prefix,
                commandName,
                args,
                cancellationToken);
        }

        private static string ExtractPlainText(string? content)
            => string.IsNullOrWhiteSpace(content)
                ? string.Empty
                : MessageContent.Parse(content).Text ?? string.Empty;
    }
}
