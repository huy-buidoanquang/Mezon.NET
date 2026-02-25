using Mezon.NET.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MezonSession = Mezon.NET.Api.Session;

public class MezonWorker : BackgroundService
{
    private readonly ILogger<MezonWorker> _logger;
    private readonly MezonClient _mezonClient;
    private readonly DateTime _startTime;

    public MezonWorker(ILogger<MezonWorker> logger)
    {
        _logger = logger;
        _startTime = DateTime.UtcNow;

        var config = new MezonSocketClientConfiguration("dev-mezon.nccsoft.vn", "8088", true);
        config.LogLevel = Mezon.NET.Logging.LogLevel.Trace;
        _mezonClient = new MezonClient(config);
        _mezonClient.Log += _mezonClient_Log;
    }

    private Task _mezonClient_Log(Mezon.NET.Logging.LogMessage arg)
    {
        AddConsoleLogging(arg);
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var res = await _mezonClient.RestClient.AuthenticateEmailAsync("", "");

            var session = new MezonSession(res);
            await _mezonClient.LoginAsync(session);
            await _mezonClient.ConnectAsync();
            await _mezonClient.JoinClanChat(1775732550744936448);

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
    public void AddConsoleLogging(Mezon.NET.Logging.LogMessage message)
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

    private ConsoleColor GetConsoleColor(Mezon.NET.Logging.LogLevel severity)
    {
        return severity switch
        {
            Mezon.NET.Logging.LogLevel.Critical => ConsoleColor.Magenta,
            Mezon.NET.Logging.LogLevel.Error => ConsoleColor.Red,
            Mezon.NET.Logging.LogLevel.Warning => ConsoleColor.Yellow,
            Mezon.NET.Logging.LogLevel.Information => ConsoleColor.White,
            Mezon.NET.Logging.LogLevel.Debug => ConsoleColor.Gray,
            Mezon.NET.Logging.LogLevel.Trace => ConsoleColor.DarkGray,
            _ => ConsoleColor.White
        };
    }
}
