using Mezon.Net.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MezonSession = Mezon.Net.Api.Session;

public class MezonWorker : BackgroundService
{
    private readonly ILogger<MezonWorker> _logger;
    private readonly MezonClient _mezonClient;
    private readonly DateTimeOffset _startTime;

    public MezonWorker(ILogger<MezonWorker> logger)
    {
        _logger = logger;
        _startTime = DateTimeOffset.UtcNow;

        var config = new MezonSocketClientOptions("dev-mezon.nccsoft.vn", "8088", true);
        config.LogLevel = Mezon.Net.Logging.LogLevel.Trace;
        _mezonClient = new MezonClient(config);
        _mezonClient.Log += _mezonClient_Log;
    }

    private Task _mezonClient_Log(Mezon.Net.Logging.LogMessage arg)
    {
        AddConsoleLogging(arg);
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var res = await _mezonClient.AuthenticateEmailAsync("", "");

            var session = new MezonSession(res);
            await _mezonClient.LoginAsync(session);
            await _mezonClient.ConnectAsync();
            await _mezonClient.JoinClanChat(2041858765849890816);

            _logger.LogInformation("Successfully initialized. Starting main loop...");

            while (!stoppingToken.IsCancellationRequested)
            {
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
        }
        finally
        {
        }
    }

    /// <summary>
    ///     Adds console logging to the LogManager.
    /// </summary>
    /// <param name="logManager">The LogManager to attach the console writer to.</param>
    /// <returns>The LogManager for chaining.</returns>
    public void AddConsoleLogging(Mezon.Net.Logging.LogMessage message)
    {
        var formatted = message.ToString(
            prependTimestamp: true,
            timestampKind: DateTimeKind.Utc,
            padSource: 20,
            fullException: true
        );

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = GetConsoleColor(message.Level);
        Console.WriteLine(formatted);
        Console.ForegroundColor = originalColor;
    }

    private ConsoleColor GetConsoleColor(Mezon.Net.Logging.LogLevel severity)
    {
        return severity switch
        {
            Mezon.Net.Logging.LogLevel.Critical => ConsoleColor.Magenta,
            Mezon.Net.Logging.LogLevel.Error => ConsoleColor.Red,
            Mezon.Net.Logging.LogLevel.Warning => ConsoleColor.Yellow,
            Mezon.Net.Logging.LogLevel.Information => ConsoleColor.White,
            Mezon.Net.Logging.LogLevel.Debug => ConsoleColor.Gray,
            Mezon.Net.Logging.LogLevel.Trace => ConsoleColor.DarkGray,
            _ => ConsoleColor.White
        };
    }
}
