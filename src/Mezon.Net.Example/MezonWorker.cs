using Mezon.Net.Example.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mezon.Net.Example;

public sealed class MezonWorker : BackgroundService
{
    private readonly ILogger<MezonWorker> _logger;
    private readonly MezonExampleOptions _options;
    private readonly IHostApplicationLifetime _lifetime;

    public MezonWorker(
        ILogger<MezonWorker> logger,
        IOptions<MezonExampleOptions> options,
        IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _options = options.Value;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ExampleRunner.RunAsync(_options, _logger, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Example cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Example run failed.");
            Environment.ExitCode = 1;
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
