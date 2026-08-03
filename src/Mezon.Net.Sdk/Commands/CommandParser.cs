using System;
using System.Collections.Generic;
using System.Text;

namespace Mezon.Net.Sdk.Commands
{
    public readonly struct ParsedCommand
    {
        public ParsedCommand(string name, IReadOnlyList<string> args)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Args = args ?? throw new ArgumentNullException(nameof(args));
        }

        public string Name { get; }
        public IReadOnlyList<string> Args { get; }
    }

    public sealed class CommandParser
    {
        public CommandParser(CommandCasePolicy casePolicy = CommandCasePolicy.IgnoreCase)
        {
            CasePolicy = casePolicy;
        }

        public CommandCasePolicy CasePolicy { get; set; }

        public bool TryParse(string text, string prefix, out ParsedCommand command)
        {
            command = default;
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrEmpty(prefix))
            {
                return false;
            }

            if (!text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            var body = text[prefix.Length..].TrimStart();
            if (body.Length == 0)
            {
                return false;
            }

            if (!TryTokenize(body, out var tokens) || tokens.Count == 0)
            {
                return false;
            }

            var name = NormalizeName(tokens[0]);
            if (name.Length == 0)
            {
                return false;
            }

            IReadOnlyList<string> args = tokens.Count > 1
                ? tokens.GetRange(1, tokens.Count - 1)
                : Array.Empty<string>();
            command = new ParsedCommand(name, args);
            return true;
        }

        internal string NormalizeName(string name)
            => CasePolicy == CommandCasePolicy.IgnoreCase
                ? name.ToLowerInvariant()
                : name;

        internal static bool TryTokenize(string input, out List<string> tokens)
        {
            tokens = new List<string>();
            if (input.Length == 0)
            {
                return false;
            }

            var index = 0;
            while (index < input.Length)
            {
                while (index < input.Length && char.IsWhiteSpace(input[index]))
                {
                    index++;
                }

                if (index >= input.Length)
                {
                    break;
                }

                if (!TryReadToken(input, ref index, out var token))
                {
                    return false;
                }

                tokens.Add(token);
            }

            return tokens.Count > 0;
        }

        private static bool TryReadToken(string input, ref int index, out string token)
        {
            token = string.Empty;

            if (input[index] == '"' || input[index] == '\'')
            {
                var quote = input[index];
                index++;
                var builder = new StringBuilder();
                while (index < input.Length)
                {
                    if (input[index] == quote)
                    {
                        token = builder.ToString();
                        index++;
                        return true;
                    }

                    if (input[index] == '\\' && index + 1 < input.Length)
                    {
                        index++;
                        builder.Append(input[index]);
                        index++;
                        continue;
                    }

                    builder.Append(input[index]);
                    index++;
                }

                return false;
            }

            var start = index;
            while (index < input.Length && !char.IsWhiteSpace(input[index]))
            {
                index++;
            }

            token = input[start..index];
            return token.Length > 0;
        }
    }
}
