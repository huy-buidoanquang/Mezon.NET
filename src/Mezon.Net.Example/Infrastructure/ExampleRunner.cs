using Mezon.Net.Example;
using Mezon.Net.Example.Diagnostics;
using Mezon.Net.Example.Scenarios;
using Microsoft.Extensions.Logging;

namespace Mezon.Net.Example.Infrastructure;

internal static class ExampleRunner
{
    private delegate Task ExampleHandler(MezonExampleOptions options, ILogger logger, CancellationToken cancellationToken);

    private static readonly Dictionary<string, ExampleHandler> Handlers = new(StringComparer.OrdinalIgnoreCase)
    {
        [ExampleModes.Verify] = SocketVerification.RunAsync,
        [ExampleModes.AllApis] = RunAllApisAsync,
        [ExampleModes.ListChannelDescs] = ListChannelDescsDiagnostic.RunAsync,
        [ExampleModes.SocketIdle] = SocketIdleDiagnostic.RunAsync,
        [ExampleModes.HeartbeatApi] = HeartbeatDuringApiDiagnostic.RunAsync,
        [ExampleModes.ListRoles] = ListRolesWireDebugDiagnostic.RunAsync,
        [ExampleModes.WireDebug] = ListRolesWireDebugDiagnostic.RunAsync,
        [ExampleModes.ListChannelMessages] = ListChannelMessagesDiagnostic.RunAsync,
        [ExampleModes.FailedApis] = FailedApisDiagnostic.RunAsync,
    };

    public static async Task RunAsync(MezonExampleOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        var mode = ExampleHelpers.ResolveMode(options);
        if (!Handlers.TryGetValue(mode, out var handler))
        {
            var available = string.Join(", ", Handlers.Keys.OrderBy(k => k));
            throw new InvalidOperationException($"Unknown example mode '{mode}'. Available: {available}");
        }

        logger.LogInformation("Running example mode: {Mode}", mode);
        await handler(options, logger, cancellationToken).ConfigureAwait(false);
    }

    private static Task RunAllApisAsync(MezonExampleOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        options.ProbeOnly = true;
        return SocketVerification.RunAsync(options, logger, cancellationToken);
    }
}
