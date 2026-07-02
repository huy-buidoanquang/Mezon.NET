using Mezon.Net.Example;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<MezonExampleOptions>(context.Configuration.GetSection(MezonExampleOptions.SectionName));
                services.AddHostedService<MezonWorker>();
            })
            .Build();

        await host.RunAsync().ConfigureAwait(false);
    }
}
