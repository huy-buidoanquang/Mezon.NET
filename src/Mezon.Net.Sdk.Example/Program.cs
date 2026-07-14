using Mezon.Net.Models;
using Mezon.Net.Sdk;
using Microsoft.Extensions.Logging;

internal class Program
{
    private const long TargetClanId = 2050100607154393088L;
    private const long TargetChannelId = 2050100608064557056L;

    private static async Task Main(string[] args)
    {
        using ILoggerFactory factory = LoggerFactory.Create(builder => { builder.AddConsole(); builder.SetMinimumLevel(LogLevel.Trace); });
        ILogger logger = factory.CreateLogger("Program");

        var botId = 2061341035941859328;
        var token = "ft4Vr4AmhyPSUMaD";
        static void WireClientLog(MezonClient client, ILogger logger)
        {
            client.Log += message =>
            {
                var text = message.ToString(prependTimestamp: true, timestampKind: DateTimeKind.Utc);
                switch (message.Level)
                {
                    case Mezon.Net.Logging.LogLevel.Trace:
                        logger.LogDebug("{MezonLog}", text);
                        break;
                    case Mezon.Net.Logging.LogLevel.Debug:
                        logger.LogDebug("{MezonLog}", text);
                        break;
                    case Mezon.Net.Logging.LogLevel.Warning:
                        logger.LogWarning("{MezonLog}", text);
                        break;
                    case Mezon.Net.Logging.LogLevel.Error:
                    case Mezon.Net.Logging.LogLevel.Critical:
                        logger.LogError("{MezonLog}", text);
                        break;
                    default:
                        logger.LogInformation("{MezonLog}", text);
                        break;
                }

                return Task.CompletedTask;
            };
        }

        var options = new MezonClientOptions(botId, token);
        options.LogLevel = Mezon.Net.Logging.LogLevel.Trace;
        await using var client = new MezonClient(options);
        WireClientLog(client, logger);
        client.ChannelMessageReceived += evt =>
        {
            var message = (ChannelMessageResponse)evt;
            Console.WriteLine($"[{message.ChannelId}] {message.Username}: {message.Content}");
            return Task.CompletedTask;
        };

        if (!await client.LoginAsync())
        {
            Console.WriteLine("Login failed.");
            return;
        }

        try
        {
            var channel = await client.GetChannelAsync(TargetChannelId);
            //for (int i = 0; i < 100; i++)
            //{
            var ack = await channel.SendAsync("{\"t\":\"Lorem ipsum dolor sit amet consectetur adipiscing elit quisqusum dolor sit aar adipiscing elit quisque fauconsectect12345678cpsum dconseconsectectconsectectconsectectctectconsectectconsectectconsectectconsectectolor sit consectectconsectectconsectectconsectectamet consectectconsectectconsectectconsectectconsectecteturr sit amet consectectetur  adipiscing elit quisque faucpsum dolor sit amet consectectetur adipiscing elit quisque faucpsum dolor sit amet consectectetur adipiscing elit quisque faucpsum dolor sit amet consectectetur adipiscing elit quisque faucpsum dolor sit amet consectectetur adipiscing elit quisque faucpsum dolor sit amet consectectetur adipiscing ecpsum dolor sit amet consectectetur adipiscing elit quisque faucpsum dolocpsum dolor sit amet consectectetur adipiscing elit quisque faucpsum dolocpsum dolor sit amet consectectetur adipiscing elit quisque faucpsum dolocpsum dolor sit amet consectectetur adipiscing elit quisque faucpsum dolocpsum dolor sit amet consectectetur adipiscing elit quisque faucpsum dolocpsum dolor sit amet consectectetur adipiscing elit quisque faucpsum dolocpsum dolor sit amet consectectetur adipiscing elit quisque faucpsum dolocpsum dolor sit amet consectectetur adipiscing elit quisque faucpsum dolocpsum dolor sit amet consectectetur adipiscing elit quisque faucpsum dolocpsum dolor sit amet consectectetur adtetuuisque faucsum dolor sit amensectetur adipiscing elit quisque faucsum dolor sit amensectetur adipiscing elit quisque faucsum dolor sit amensectetur adipiscing elit quisque faucsum dolor sit amensectetur adipiscing elit quisque faucsum dolor sit amensectetur adipiscing elit quisque faucsum dolor sit amensectetur adipiscing elit quisque faucsum dolor sit amensectetur adipiscing elit quisque faucsum dolor sit amensectetur adipiscing elit quisque faucsum dolor sit amensectetur adipiscing elit quisque faucsum dolor sit amensectetur adipiscing elit qunisl malesuada lacinia integer nunc posuere ut hendrerit semper vel class aptent taciti sociosqu ad litora torquent per conubia nostra inceptos himenaeos orci varius natoque penatibus et magnis dis parturient montes nascetur ridiculus mus donec rhoncus eros lobortis nulla molestie mattis scelerisque maximus eget fermentum odio phasellus non purus est efficitur laoreet mauris pharetra vestibulum fusce dictum risus blandit quis suspendisse aliquet nisi sodales consequat magna ante condimentum neque at luctus nibh finibus facilisis dapibus etiam interdum tortor ligula congue sollicitudin erat viverra ac tincidunt nam porta elementum a enim euismod quam justo lectus commodo augue arcu dignissim velit aliquam imperdiet mollis nullam volutpat porttitor ullamcorper rutrum gravida cras eleifend turpis fames primis vulputate ornare sagittis vehicula praesent dui felis venenatis ultrices proin libero feugiat tristique accumsan maecenas potenti ultricies habitant morbi senectus netus suscipit auctor curabitur facilisi cubilia curae hac habitasse platea dictumst lorem ipsum dolor sit amet consectetur adipiscing elit quisque faucibus ex sapien vitae pellentesque sem placerat in id cursus mi pretium tellus duis convallis tempus leo eu aenean sed diam urna tempor pulvinar vivamus fringilla lacus nec metus bibendum egestas iaculis massa nisl malesuada lacinia integer nunc posuere ut hendrerit semper vel class aptent taciti sociosqu ad litora torquent per conubia nostra inceptos himenaeos orci varius natoque penatibus et magnis dis parturient montes nascetur ridiculus mus donec rhoncus eros lobortis nulla molestie mattis scelerisque maximus eget fermentum odio phasellus non purus est efficitur laoreet mauris pharetra vestibulum fusce dictum risus blandit quis suspendisse aliquet nisi sodales consequat magna ante condimentum neque at luctus nibh finibus facilisis dapibus etiam interdum tortor ligula congue sollicitudin erat viverra ac tincidunt nam porta elementum a enim euismod quam justo lectus commodo augue arcu dignissim velit aliquam imperdiet mollis nullam volutpat porttitor ullamcorper rutrum gravida cras eleifend turpis fames primis vulputate ornare sagittis vehicula praesent dui felis venenatis ultrices proin libero feugiaisque faucsum dolor sit amensectetur adipiscing elit quisque faucsum dolor sit ame adipiscing elit quisque faucsum dolor sit amet consectetur adipiscing elit quisqueum dolor sit amet consectetur adipiscing elit quisqu faucsum dolor sit amet consectetur adipiscing elit quisque fauce faucibus ex sapien vitae pellentesque sem placerat in id cursus mi pretium tellus duis convallis tempus leo eu aenean sed diam urna tempor pulvinar vivamus fringilla lacus nec metus bibendum egestas iaculis massa nisl malesuada lacinia integer nunc posuere ut hendrerit semper vel class aptent taciti sociosqu ad litora torquent per conubia nostra inceptos himenaeos orci varius natoque penatibus et magnis dis parturient montes nascetur ridiculus mus donec rhoncus eros lobortis nulla molestie mattis scelerisque maximus eget fermentum odio phasellus non purus est efficitur laoreet mauris pharetra vestibulum fusce dictum risus blandit quis suspendisse aliquet nisi sodales consequat magna ante condimentum neque at luctus nibh finibus facilisis dapibus etiam interdum tortor ligula congue sollicitudin erat viverra ac tincidunt nam porta elementum a enim euismod quam justo lectus commodo augue arcu dignissim velit aliquam imperdiet mollis nullam volutpat porttitor ullamcorper rutrum gravida cras eleifend turpis fames primis vulputate ornare sagittis vehicula praesent dui felis venenatis ultrices proin libero feugiat tristique accumsan maecenas potenti ultricies habitant morbi senectus netus suscipit auctor curabitur facilisi cubilia curae hac habitasse platea dictumst lorem ipsum dolor sit amet consectetur adipiscing elit quisque faucibus ex sapien vitae pellentesque sem placerat in id cursus mi pretium tellus duis convallis tempus leo eu aenean sed diam urna tempor pulvinar vivamus fringilla lacus nec metus bibendum egestas iaculis massa nisl malesuada lacinia integer nunc posuere ut hendrerit semper vel class aptent taciti sociosqu ad litora torquent per conubia nostra inceptos himenaeos orci varius natoque penatibus et magnis dis parturient montes nascetur ridiculus mus donec rhoncus eros lobortis nulla molestie mattis scelerisque maximus eget fermentum odio phasellus non purus est efficitur laoreet mauris pharetra vestibulum fusce dictum risus blandit quis suspendisse aliquet nisi sodales consequat magna ante condimentum neque at luctus nibh finibus facilisis dapibus etiam interdum tortor ligula congue sollicitudin erat viverra ac tincidunt nam porta elementum a enim euismod quam justo lectus commodo augue arcu dignissim velit aliquam imperdiet mollis nullam volutpat porttitor ullamcorper rutrum gravida cras eleifend turpis fames primis vulputate ornare sagittis vehicula praesent dui felis venenatis ultrices proin libero feugiat tristique accumsan maecenas potenti ultricies habitant morbi senectus netus suscipit auctor curabitur facilisi cubilia curae hac habitasse platea dictumst lorem ipsum dolor sit amet consectetur adipiscing elit quisque faucibus ex sapien vitae pellentesque sem placerat in id cursus mi pretium tellus duis convallis tempus leo eu aenean sed diam urna tempor pulvinar vivamus fringilla lacus nec metus bibendum egestas iaculis massa nisl malesuada lacinia integer nunc posuere ut hendrerit semper vel class aptent taciti sociosqu ad litora torquent per conubia nostra inceptos himenaeos orci varius natoque penatibus et magnis dis parturient montes nascetur ridiculus mus donec rhoncus eros lobortis nulla molestie mattis scelerisque maximus eget fermentum odio phasellus non purus est efficitur laoreet mauris pharetra vestibulum fusce dictum risus blandit quis suspendisse aliquet nisi sodales consequat magna ante condimentum neque at luctus nibh finibus facilisis dapibus etiam interdum tortor ligula congue sollicitudin erat viverra ac tincidunt nam porta elementum a enim euismod quam justo lectus commodo augue.justo lectus commodo auguusto lectus commodo augue.justo lectus commodo augue.\"}").ConfigureAwait(false);
            Console.WriteLine($"Sent to clan {TargetClanId}, channel {TargetChannelId}: message_id={ack.MessageId}");
            //}
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Send failed: {ex.Message}");
            logger.LogError(ex, "Failed to send message to channel {ChannelId}", TargetChannelId);
        }

        Console.WriteLine("Bot connected. Press Ctrl+C to exit.");
        await Task.Delay(Timeout.Infinite);
    }
}
