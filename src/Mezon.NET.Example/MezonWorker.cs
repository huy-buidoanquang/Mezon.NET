using Mezon.NET.Api;
using Mezon.NET.Core;
using Mezon.NET.Logging;
using Mezon.NET.WebSocket;
using Mezon.Protobuf.Api;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MezonSession = Mezon.NET.Api.Session;

public class MezonWorker : BackgroundService
{
    private readonly ILogger<MezonWorker> _logger;
    private readonly Mezon.NET.WebSocket.MezonClient _mezonClient;
    private readonly FileLogWriter? _fileLogWriter;
    private readonly DateTime _startTime;

    public MezonWorker(ILogger<MezonWorker> logger)
    {
        _logger = logger;
        _startTime = DateTime.UtcNow;

        // Create a shared LogManager with both console and file logging
        //var (logManager, fileWriter) = LogManagerFactory.CreateWithLogging(
        //    logSeverity: LogSeverity.Verbose,
        //    logFilePath: $"logs/mezon-{DateTime.Now:yyyy-MM-dd}.log",
        //    enableConsole: true,
        //    enableFile: true
        //);
        //_fileLogWriter = fileWriter;

        // Create MezonClient with the shared LogManager
        var config = new MezonSocketClientConfiguration("dev-mezon.nccsoft.vn", "8088", true);
        _mezonClient = new Mezon.NET.WebSocket.MezonClient(config);

        // Subscribe to MezonClient's log events to forward to Microsoft.Extensions.Logging
        _mezonClient.Log += message =>
        {
            return Task.CompletedTask;
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var res = await _mezonClient.RestClient.AuthenticateEmailAsync("pocolomos@gmail.com", "C0nandoiner123$");

            var session = new MezonSession(res);
            await _mezonClient.LoginAsync(session);
            await _mezonClient.ConnectAsync();
            await _mezonClient.JoinClanChat(1775732550744936448);
            //var listedApps = await _mezonClient.Rest.CreateQRLoginAsync(new LoginIDRequest());
            //var grpc = new DefaultGRPCClient("https://dev-mezon.nccsoft.vn:7305");
            //grpc.SetHeader("Authorization", "Bearer " + session.AuthToken);
            //var listedApps = await grpc.Client.ListClanDescsAsync(new ListClanDescRequest
            //{
            //    Limit = 50
            //}, grpc.GetCallOptions());

            //Console.WriteLine($"Fetched {listedApps.LoginId} clan descriptions.");


            // Subscribe to configured clans
            //foreach (var clanId in _workerConfig.SubscribedClans)
            //{
            //    await _mezonClient.SocketManager.JoinClanChatAsync(clanId);
            //    _logger.LogInformation("Joined clan: {ClanId}", clanId);
            //}

            _logger.LogInformation("Successfully initialized. Starting main loop...");

            while (!stoppingToken.IsCancellationRequested)
            {
                // --- ĐẶT LOGIC LẮNG NGHE SOCKET CỦA BẠN VÀO ĐÂY ---
                // Ví dụ: var data = await _mezonClient.ReceiveDataAsync(stoppingToken);
                // Process message and increment counter
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
            // Clean up file writer on shutdown
            _fileLogWriter?.Dispose();
        }
    }
}
