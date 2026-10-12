// Joins a Mezon SFU room as a speaker, plays an Ogg Opus file and echoes chat.
//
//   MEZON_SFU_URL=wss://sfu.example/ws MEZON_SFU_TOKEN=<meet token> \
//     dotnet run --project src/Mezon.Net.Sdk.Sfu.Example.Bot -- --ogg music.opus --loop --echo
//
// The token comes from the environment so it never shows up in the process list or shell history.
// Pass --bind-address 127.0.0.1 when the SFU runs on this machine's loopback.

using System.Diagnostics;
using System.Net;
using Mezon.Net.Sdk.Sfu;

namespace Mezon.Net.Sdk.Sfu.Example.Bot;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        string? ogg = null;
        var looping = false;
        var echo = false;
        TimeSpan? duration = null;
        IPAddress? bind = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--ogg" when i + 1 < args.Length:
                    ogg = args[++i];
                    break;
                case "--loop":
                    looping = true;
                    break;
                case "--echo":
                    echo = true;
                    break;
                case "--duration" when i + 1 < args.Length:
                    duration = TimeSpan.FromSeconds(double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "--bind-address" when i + 1 < args.Length:
                    bind = IPAddress.Parse(args[++i]);
                    break;
                default:
                    Console.Error.WriteLine($"ignoring unknown argument {args[i]}");
                    break;
            }
        }

        var url = Environment.GetEnvironmentVariable("MEZON_SFU_URL");
        var token = Environment.GetEnvironmentVariable("MEZON_SFU_TOKEN");
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(token))
        {
            Console.Error.WriteLine("set MEZON_SFU_URL and MEZON_SFU_TOKEN");
            return 2;
        }

        await using var room = new Room(new RoomOptions { BindAddress = bind });
        try
        {
            await room.ConnectAsync(url, TokenProvider.Fixed(token));
            await room.LocalParticipant.SetMicrophoneEnabledAsync(true);
        }
        catch (SfuException e)
        {
            Console.Error.WriteLine($"connect failed: {e.Message}");
            return 1;
        }

        Console.WriteLine($"connected to room {room.RoomId} as peer {room.LocalParticipant.PeerId}");
        using var stop = new CancellationTokenSource();
        if (duration is { } d)
        {
            stop.CancelAfter(d);
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.Cancel();
        };

        if (ogg is not null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await PlayAsync(room.LocalParticipant.Microphone, ogg, looping, stop.Token);
                    Console.WriteLine("playback finished");
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e) when (e is IOException or InvalidDataException)
                {
                    Console.Error.WriteLine($"playback failed: {e.Message}");
                }
            });
        }

        var code = 0;
        try
        {
            await foreach (var e in room.Events.ReadAllAsync(stop.Token))
            {
                switch (e)
                {
                    case ParticipantConnected p:
                        Console.WriteLine($"joined: {p.Participant.Info.DisplayName} (peer {p.Participant.PeerId})");
                        break;
                    case ParticipantDisconnected p:
                        Console.WriteLine($"left: {p.Participant.Info.DisplayName} (peer {p.Participant.PeerId})");
                        break;
                    case MessageReceived m:
                        Console.WriteLine($"message from {m.UserId}: {m.Message}");
                        if (echo && !m.Message.StartsWith("echo: ", StringComparison.Ordinal))
                        {
                            _ = EchoAsync(room, m.Message);
                        }

                        break;
                    case Disconnected dc:
                        Console.WriteLine($"disconnected: {dc.Reason}");
                        code = 1;
                        stop.Cancel();
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        await room.DisconnectAsync();
        return code;
    }

    private static async Task EchoAsync(Room room, string message)
    {
        try
        {
            await room.SendMessageAsync($"echo: {message}");
        }
        catch (SfuException e)
        {
            Console.Error.WriteLine($"echo failed: {e.Message}");
        }
    }

    /// <summary>Writes the file's Opus packets in real time (each packet is due at the sum of the previous durations).</summary>
    private static async Task PlayAsync(LocalAudioTrack microphone, string path, bool looping, CancellationToken cancellationToken)
    {
        do
        {
            await using var file = File.OpenRead(path);
            var reader = new OggOpusReader(file);
            var clock = Stopwatch.StartNew();
            var due = TimeSpan.Zero;
            while (reader.ReadPacket() is { } packet)
            {
                var wait = due - clock.Elapsed;
                if (wait > TimeSpan.FromMilliseconds(1))
                {
                    await Task.Delay(wait, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                microphone.WriteOpus(packet.Packet, packet.Duration);
                due += packet.Duration;
            }
        }
        while (looping);
    }
}
