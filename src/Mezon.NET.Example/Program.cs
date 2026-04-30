using Mezon.Net.Example;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class Program
{
    public static async Task Main(string[] args)
    {
        await Transport.Run();
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                //services.ConfigureMezonApiClient(context.Configuration);

                //services.AddHostedService<MezonWorker>();
            })
            .Build();

        await host.RunAsync();
    }
}
