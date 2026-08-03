using Mezon.Net.Sdk.Example;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        BotOptions options;
        try
        {
            options = BotOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            BotOptions.PrintHelp();
            return 2;
        }

        if (options.ShowHelp)
        {
            BotOptions.PrintHelp();
            return 0;
        }

        try
        {
            options.Validate();
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            BotOptions.PrintHelp();
            return 2;
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(ToMsLogLevel(options.LogLevel));
            builder.AddSimpleConsole(console =>
            {
                console.TimestampFormat = "HH:mm:ss ";
                console.SingleLine = true;
            });
        });

        var logger = loggerFactory.CreateLogger("Mezon.Net.Sdk.Example");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            if (!cts.IsCancellationRequested)
            {
                logger.LogInformation("Ctrl+C received.");
                cts.Cancel();
            }
        };

        try
        {
            var bot = new MezonBot(options, logger);
            return await bot.RunAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Bot terminated unexpectedly.");
            return 1;
        }
    }

    private static MsLogLevel ToMsLogLevel(Mezon.Net.Logging.LogLevel level)
        => level switch
        {
            Mezon.Net.Logging.LogLevel.Trace => MsLogLevel.Trace,
            Mezon.Net.Logging.LogLevel.Debug => MsLogLevel.Debug,
            Mezon.Net.Logging.LogLevel.Information => MsLogLevel.Information,
            Mezon.Net.Logging.LogLevel.Warning => MsLogLevel.Warning,
            Mezon.Net.Logging.LogLevel.Error => MsLogLevel.Error,
            Mezon.Net.Logging.LogLevel.Critical => MsLogLevel.Critical,
            _ => MsLogLevel.Information,
        };
}
