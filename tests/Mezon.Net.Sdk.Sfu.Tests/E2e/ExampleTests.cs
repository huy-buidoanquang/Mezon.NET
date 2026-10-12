using System.Diagnostics;
using static Mezon.Net.Sdk.Sfu.Tests.E2e.E2e;

namespace Mezon.Net.Sdk.Sfu.Tests.E2e;

/// <summary>Runs the example programs (built with the tests) against the real SFU, the way a user would.</summary>
[Collection("e2e")]
public class ExampleTests
{
    private static string ExampleDll(string name)
    {
        // The test project references the examples, so their assemblies are next to the tests.
        var dll = Path.Combine(AppContext.BaseDirectory, $"Mezon.Net.Sdk.Sfu.Example.{name}.dll");
        Assert.True(File.Exists(dll), $"{dll} not built");
        return dll;
    }

    private static Process Start(string name, Stack stack, string token, params string[] args)
    {
        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add(ExampleDll(name));
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        if (stack.Loopback)
        {
            info.ArgumentList.Add("--bind-address");
            info.ArgumentList.Add("127.0.0.1");
        }

        info.Environment["MEZON_SFU_URL"] = stack.WsUrl;
        info.Environment["MEZON_SFU_TOKEN"] = token;
        return Process.Start(info) ?? throw new InvalidOperationException($"could not start the {name} example");
    }

    [E2eFact("dynamic")]
    public async Task The_recorder_writes_each_speaker_to_its_own_file()
    {
        var stack = Stack.Get("dynamic")!;
        var roomId = Peer.RandomId();
        var speakers = new List<(Peer Peer, byte Marker)>();
        var output = Path.Combine(Path.GetTempPath(), $"sfu-recorder-{Peer.RandomId()}");
        try
        {
            foreach (var (name, marker) in new[] { ("A", (byte)'A'), ("B", (byte)'B') })
            {
                var peer = await Join(stack, roomId, name);
                await peer.Publish(marker, () => null);
                speakers.Add((peer, marker));
            }

            var token = stack.Token(Peer.RandomId(), roomId, "Recorder;", TimeSpan.FromMinutes(10));
            using var recorder = Start("Recorder", stack, token, "--out", output, "--duration", "8");
            var stdout = recorder.StandardOutput.ReadToEndAsync();
            var stderr = recorder.StandardError.ReadToEndAsync();
            await recorder.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token);
            Assert.True(recorder.ExitCode == 0, $"recorder exited with {recorder.ExitCode}: {await stdout} {await stderr}");
            var files = Directory.GetFiles(output);
            Assert.True(files.Length == 2, string.Join(", ", files));
            foreach (var (peer, marker) in speakers)
            {
                var file = files.Single(f => Path.GetFileName(f).StartsWith($"{peer.UserId}-p", StringComparison.Ordinal));
                await using var stream = File.OpenRead(file);
                var reader = new OggOpusReader(stream);
                var (count, last) = (0, -1L);
                while (reader.ReadPacket() is { } packet)
                {
                    var frame = Peer.ParseFrame(packet.Packet);
                    Assert.NotNull(frame);
                    Assert.True(frame!.Value.Marker == marker && frame.Value.Seq > last && packet.Duration == TimeSpan.FromMilliseconds(20), $"{frame.Value.Marker} {frame.Value.Seq} after {last}");
                    last = frame.Value.Seq;
                    count++;
                }

                Assert.True(count > 100, $"only {count} frames in {file}");
            }
        }
        finally
        {
            foreach (var (peer, _) in speakers)
            {
                await peer.DisposeAsync();
            }

            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }

    [E2eFact("dynamic")]
    public async Task The_bot_plays_the_file_and_echoes_chat()
    {
        var stack = Stack.Get("dynamic")!;
        var path = Path.Combine(Path.GetTempPath(), $"sfu-bot-{Peer.RandomId()}.opus");
        try
        {
            await using (var file = File.Create(path))
            {
                using var writer = new OggOpusWriter(file, 1);
                for (uint seq = 0; seq < 400; seq++)
                {
                    writer.WritePacket(Peer.OpusFrame((byte)'B', seq));
                }
            }

            var roomId = Peer.RandomId();
            await using var listener = await Join(stack, roomId, "Listener");
            var botId = Peer.RandomId();
            var token = stack.Token(botId, roomId, "Bot;", TimeSpan.FromMinutes(10));
            using var bot = Start("Bot", stack, token, "--ogg", path, "--echo", "--duration", "16");
            var stdout = bot.StandardOutput.ReadToEndAsync();
            var stderr = bot.StandardError.ReadToEndAsync();
            await WaitFor(15, "the bot", () => listener.Room.GetParticipantsByUserId(botId).Count == 1);
            await listener.Room.SendMessageAsync("hello bot");
            await WaitFor(10, "the echo", () => listener.Messages.Contains($"{botId}: echo: hello bot"));

            // The listener subscribes once the server renegotiated for the bot, so the start may be missed.
            await WaitFor(15, "the end of the file", () => listener.SequenceRange(botId, (byte)'B')?.Last == 399);
            var first = listener.SequenceRange(botId, (byte)'B')!.Value.First;
            var expected = 400 - first;
            Assert.True(listener.Frames(botId, (byte)'B') * 100 >= expected * 95, $"received {listener.Frames(botId, (byte)'B')} of the {expected} frames since the first");
            await bot.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);
            var output = await stdout;
            Assert.True(bot.ExitCode == 0, $"bot exited with {bot.ExitCode}: {output} {await stderr}");
            Assert.Contains("connected to room", output);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
