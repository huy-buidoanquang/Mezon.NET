// Joins a Mezon SFU room as audience and records every participant's audio to its own Ogg Opus file, without
// decoding it.
//
//   MEZON_SFU_URL=wss://sfu.example/ws MEZON_SFU_TOKEN=<meet token> \
//     dotnet run --project src/Mezon.Net.Sdk.Sfu.Example.Recorder -- --out recordings --duration 60
//
// One file per subscription: <userId>-p<peerId>-<n>.opus. A participant who reconnects, or who moves to another
// slot in fixed-slot rooms, gets a new file. Pass --bind-address 127.0.0.1 when the SFU runs on this machine's
// loopback.

using System.Net;

namespace Mezon.Net.Sdk.Sfu.Example.Recorder;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var output = "recordings";
        TimeSpan? duration = null;
        IPAddress? bind = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length:
                    output = args[++i];
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

        Directory.CreateDirectory(output);
        await using var room = new Room(new RoomOptions { Role = Role.Audience, BindAddress = bind });
        try
        {
            await room.ConnectAsync(url, TokenProvider.Fixed(token));
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

        var recordings = new List<Task>();
        var files = 0;
        var code = 0;
        try
        {
            await foreach (var e in room.Events.ReadAllAsync(stop.Token))
            {
                switch (e)
                {
                    case TrackSubscribed { Track.Source: TrackSource.Microphone } subscribed:
                        files++;
                        var path = Path.Combine(output, $"{subscribed.Participant.UserId}-p{subscribed.Participant.PeerId}-{files}.opus");
                        Console.WriteLine($"recording {subscribed.Participant.Info.DisplayName} -> {path}");
                        recordings.Add(Task.Run(() => RecordAsync(subscribed.Track, path)));
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

        // Leaving ends every track, which lets each recording finish its file.
        await room.DisconnectAsync();
        await Task.WhenAll(recordings);
        return code;
    }

    private static async Task RecordAsync(RemoteTrack track, string path)
    {
        var packets = 0;
        await using (var file = File.Create(path))
        {
            using var writer = new OggOpusWriter(file, 2);
            await foreach (var packet in track.ReadAllAsync())
            {
                if (packet.Payload.IsEmpty)
                {
                    continue;
                }

                try
                {
                    writer.WritePacket(packet.Payload.Span);
                    packets++;
                }
                catch (InvalidDataException e)
                {
                    Console.Error.WriteLine($"record {path}: {e.Message}");
                }
            }
        }

        Console.WriteLine($"saved {path} ({packets} packets)");
    }
}
