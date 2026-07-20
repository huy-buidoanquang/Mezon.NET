using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mezon.Net.Sdk.Commands
{
    public delegate Task CommandHandler(ICommandContext context);
    public delegate bool CommandAuthorizePredicate(ICommandContext context);
    public delegate ValueTask<bool> CommandAuthorizeAsyncPredicate(ICommandContext context);

    public sealed class CommandInfo
    {
        internal CommandInfo(string name, CommandHandler handler)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public string Name { get; }
        public CommandHandler Handler { get; }
        public IReadOnlyList<string> Aliases { get; internal set; } = Array.Empty<string>();
        public TimeSpan? Cooldown { get; internal set; }
        public CommandAuthorizePredicate? Authorize { get; internal set; }
        public CommandAuthorizeAsyncPredicate? AuthorizeAsync { get; internal set; }

        internal bool IsAuthorized(ICommandContext context)
        {
            if (Authorize is not null && !Authorize(context))
            {
                return false;
            }

            return true;
        }

        internal async ValueTask<bool> IsAuthorizedAsync(ICommandContext context)
        {
            if (!IsAuthorized(context))
            {
                return false;
            }

            if (AuthorizeAsync is null)
            {
                return true;
            }

            return await AuthorizeAsync(context).ConfigureAwait(false);
        }
    }

    public sealed class CommandRegistration
    {
        private readonly CommandService _service;
        private readonly CommandInfo _command;

        internal CommandRegistration(CommandService service, CommandInfo command)
        {
            _service = service;
            _command = command;
        }

        public CommandRegistration WithAlias(params string[] aliases)
        {
            _service.AddAliases(_command, aliases);
            return this;
        }

        public CommandRegistration WithCooldown(TimeSpan cooldown)
        {
            _command.Cooldown = cooldown;
            return this;
        }

        public CommandRegistration Require(CommandAuthorizePredicate predicate)
        {
            _command.Authorize = predicate;
            return this;
        }

        public CommandRegistration RequireAsync(CommandAuthorizeAsyncPredicate predicate)
        {
            _command.AuthorizeAsync = predicate;
            return this;
        }
    }
}
