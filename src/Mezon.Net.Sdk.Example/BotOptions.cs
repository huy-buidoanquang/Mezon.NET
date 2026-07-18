using MezonLogLevel = Mezon.Net.Logging.LogLevel;

namespace Mezon.Net.Sdk.Example;

internal sealed class BotOptions
{
    public long BotId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public long? ChannelId { get; private set; }
    public string CommandPrefix { get; private set; } = "!";
    public MezonLogLevel LogLevel { get; private set; } = MezonLogLevel.Information;
    public bool ShowHelp { get; private set; }

    public static BotOptions Parse(string[] args)
    {
        var options = new BotOptions
        {
            BotId = ParseLong(Environment.GetEnvironmentVariable("MEZON_BOT_ID")) ?? 0,
            Token = Environment.GetEnvironmentVariable("MEZON_BOT_TOKEN") ?? string.Empty,
            ChannelId = ParseLong(Environment.GetEnvironmentVariable("MEZON_CHANNEL_ID")),
            CommandPrefix = FirstNonEmpty(Environment.GetEnvironmentVariable("MEZON_COMMAND_PREFIX"), "!")!,
            LogLevel = ParseLogLevel(Environment.GetEnvironmentVariable("MEZON_LOG_LEVEL"), MezonLogLevel.Information),
        };

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;
                case "--bot-id":
                    options.BotId = RequireLong(RequireValue(args, ref i, arg), arg);
                    break;
                case "--token":
                    options.Token = RequireValue(args, ref i, arg);
                    break;
                case "--channel-id":
                    options.ChannelId = RequireLong(RequireValue(args, ref i, arg), arg);
                    break;
                case "--prefix":
                    options.CommandPrefix = RequireValue(args, ref i, arg);
                    break;
                case "--log-level":
                    options.LogLevel = ParseLogLevel(RequireValue(args, ref i, arg), MezonLogLevel.Information);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{arg}'. Use --help for usage.");
            }
        }

        if (string.IsNullOrWhiteSpace(options.CommandPrefix))
        {
            options.CommandPrefix = "!";
        }

        return options;
    }

    public void Validate()
    {
        if (BotId == 0)
        {
            throw new ArgumentException("Bot ID is required. Set MEZON_BOT_ID or pass --bot-id.");
        }

        if (string.IsNullOrWhiteSpace(Token))
        {
            throw new ArgumentException("Bot token is required. Set MEZON_BOT_TOKEN or pass --token.");
        }
    }

    public static void PrintHelp()
    {
        Console.WriteLine(
            """
            Mezon.Net.Sdk.Example — sample bot host

            Required:
              MEZON_BOT_ID / --bot-id <id>
              MEZON_BOT_TOKEN / --token <token>

            Optional:
              MEZON_CHANNEL_ID / --channel-id <id>   Restrict commands to one channel
              MEZON_COMMAND_PREFIX / --prefix <p>    Command prefix (default: !)
              MEZON_LOG_LEVEL / --log-level <level>  Trace|Debug|Information|Warning|Error|Critical
              -h, --help                            Show this help

            Commands (in-channel):
              !ping   Reply with latency and typed payload counts
              !help   Show available bot commands

            Example (PowerShell):
              $env:MEZON_BOT_ID="123"
              $env:MEZON_BOT_TOKEN="secret"
              dotnet run --project src/Mezon.Net.Sdk.Example -- --channel-id 456 --prefix !
            """);
    }

    private static string RequireValue(string[] args, ref int index, string flag)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {flag}.");
        }

        index++;
        return args[index];
    }

    private static long RequireLong(string value, string flag)
        => long.TryParse(value, out var parsed)
            ? parsed
            : throw new ArgumentException($"Invalid integer for {flag}: '{value}'.");

    private static long? ParseLong(string? value)
        => long.TryParse(value, out var parsed) ? parsed : null;

    private static MezonLogLevel ParseLogLevel(string? value, MezonLogLevel fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "trace" => MezonLogLevel.Trace,
            "debug" => MezonLogLevel.Debug,
            "info" or "information" => MezonLogLevel.Information,
            "warn" or "warning" => MezonLogLevel.Warning,
            "error" => MezonLogLevel.Error,
            "critical" or "fatal" => MezonLogLevel.Critical,
            _ => Enum.TryParse(value, ignoreCase: true, out MezonLogLevel level) ? level : fallback,
        };
    }

    private static string? FirstNonEmpty(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a) ? a : b;
}
