using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Sdk.Sfu.Core;
using Microsoft.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace Mezon.Net.Sdk.Sfu.Peer;

/// <summary>
/// The SIPSorcery PeerConnection, adapted to mezon-sfu. This is the only file that touches SIPSorcery types.
/// </summary>
/// <remarks>
/// SIPSorcery 10.0.16 quirks worked around here:
/// <list type="bullet">
/// <item>Answers use the <c>UDP/TLS/RTP/SAVP</c> profile unless <c>X_UseRtpFeedbackProfile</c> is set; mezon-sfu
/// reads payload types only after <c>"SAVPF "</c> and rejects the answer (<c>invalid_answer_sdp</c>) when the camera
/// section has none.</item>
/// <item>Media streams that <c>setRemoteDescription</c> creates for m-lines without a local track get no RTP channel,
/// SRTP context or event wiring, so their packets are silently dropped. Before every offer, a receive-only local
/// track is added for each offered m-line that has no stream yet (<c>addTrack</c> initialises streams properly).</item>
/// <item><c>createAnswer</c> resets inactive tracks to their default direction (answering an inactive offer with
/// recvonly) and adds telephone-event to audio. <see cref="Sdp.NormalizeAnswer"/> rewrites directions, payload types
/// and the BUNDLE group before the answer is applied and sent.</item>
/// <item>Received RTP is reported per stream index; the index of each kind maps to the offer's m-lines of that kind
/// in order, from which the mid is known. Streams are matched by the SSRCs the offer announces.</item>
/// <item>There is no rollback: an offer that failed half way is simply replaced by the next one.</item>
/// <item>The audio-level extension is written on every packet (SIPSorcery writes all local extensions); "no level"
/// is sent as level 0 with the voice flag, which mezon-sfu's voice activity detection treats exactly like a packet
/// without the extension (voice).</item>
/// </list>
/// </remarks>
internal sealed class SipsorceryPeer : IPeerConnection
{
    private const int OpusPt = 111;

    private readonly RTCPeerConnection _pc;
    private readonly PeerConfig _config;
    private readonly object _lock = new();
    private readonly MicrophoneUplink _microphone;
    private readonly CameraUplink? _camera;
    private uint[] _audioMids = Array.Empty<uint>();
    private uint[] _videoMids = Array.Empty<uint>();
    private Dictionary<uint, Dictionary<int, string>> _codecs = new();
    private string _offer = "";
    private string _localSdp = "";
    private HashSet<uint> _receiving = new();
    private int _closed;

    private SipsorceryPeer(PeerConfig config)
    {
        _config = config;
        var rtcConfig = new RTCConfiguration
        {
            X_UseRtpFeedbackProfile = true,
            X_BindAddress = config.BindAddress,
            iceServers = config.IceServers
                .SelectMany(s => s.Urls.Select(url => new RTCIceServer { urls = url, username = s.Username, credential = s.Credential }))
                .ToList(),
        };
        _pc = new RTCPeerConnection(rtcConfig);

        // Uplinks are added before the first offer so they pair with mid 0 (audio) and mid 1 (camera). The camera
        // m-line is never renegotiated by the server, so a speaker must answer it sendonly from the first answer.
        var microphone = new MediaStreamTrack(
            SDPMediaTypesEnum.audio,
            false,
            new List<SDPAudioVideoMediaFormat> { OpusFormat() },
            MediaStreamStatusEnum.SendOnly,
            null,
            new Dictionary<int, RTPHeaderExtension> { [1] = new AudioLevelExtension(1) })
        {
            NoDtmfSupport = true,
        };
        _pc.addTrack(microphone);
        if (config.Camera)
        {
            _pc.addTrack(new MediaStreamTrack(SDPMediaTypesEnum.video, false, new List<SDPAudioVideoMediaFormat> { new(new VideoFormat(VideoCodecsEnum.VP8, 96)) }, MediaStreamStatusEnum.SendOnly));
        }
        else
        {
            _pc.addTrack(ReceiveVideoTrack());
        }

        _pc.addTrack(ReceiveVideoTrack());
        _microphone = new MicrophoneUplink(_pc.AudioStreamList[0]);
        _camera = config.Camera ? new CameraUplink(_pc.VideoStreamList[0]) : null;
        _pc.onconnectionstatechange += OnConnectionStateChange;
        _pc.OnRtpPacketReceivedByIndex += OnRtp;
    }

    public static Task<IPeerConnection> CreateAsync(PeerConfig config, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => (IPeerConnection)new SipsorceryPeer(config), cancellationToken);
    }

    private static SDPAudioVideoMediaFormat OpusFormat() => new(new AudioFormat(AudioCodecsEnum.OPUS, OpusPt, 48000, 2, "minptime=10;useinbandfec=1"));

    private static MediaStreamTrack ReceiveAudioTrack() =>
        new(SDPMediaTypesEnum.audio, false, new List<SDPAudioVideoMediaFormat> { OpusFormat() }, MediaStreamStatusEnum.RecvOnly) { NoDtmfSupport = true };

    private static MediaStreamTrack ReceiveVideoTrack() => new(
        SDPMediaTypesEnum.video,
        false,
        new List<SDPAudioVideoMediaFormat>
        {
            new(new VideoFormat(VideoCodecsEnum.VP8, 96)),
            new(new VideoFormat(VideoCodecsEnum.VP9, 98)),
            new(new VideoFormat(VideoCodecsEnum.H264, 102)),
            new(new VideoFormat(VideoCodecsEnum.AV1, 45)),
        },
        MediaStreamStatusEnum.RecvOnly);

    private void OnConnectionStateChange(RTCPeerConnectionState state)
    {
        var mapped = state switch
        {
            RTCPeerConnectionState.connecting => PeerState.Connecting,
            RTCPeerConnectionState.connected => PeerState.Connected,
            RTCPeerConnectionState.disconnected => PeerState.Disconnected,
            RTCPeerConnectionState.failed => PeerState.Failed,
            RTCPeerConnectionState.closed => PeerState.Closed,
            _ => PeerState.New,
        };
        try
        {
            _config.OnState(mapped);
        }
        catch (Exception e)
        {
            _config.Logger.LogWarning(e, "connection state handler failed");
        }
    }

    private void OnRtp(int index, IPEndPoint remote, SDPMediaTypesEnum media, RTPPacket packet)
    {
        uint mid;
        string? mime = null;
        lock (_lock)
        {
            var mids = media == SDPMediaTypesEnum.audio ? _audioMids : _videoMids;
            if (index < 0 || index >= mids.Length)
            {
                return;
            }

            mid = mids[index];
            if (_codecs.TryGetValue(mid, out var codecs))
            {
                codecs.TryGetValue(packet.Header.PayloadType, out mime);
            }
        }

        if (mid < Sdp.RemoteMidBase)
        {
            return;
        }

        try
        {
            var rtp = new RtpPacket(
                (byte)packet.Header.PayloadType,
                packet.Header.SequenceNumber,
                packet.Header.Timestamp,
                packet.Header.SyncSource,
                packet.Header.MarkerBit != 0,
                packet.Payload);
            _config.OnRtp(mid, rtp, mime);
        }
        catch (Exception e)
        {
            _config.Logger.LogWarning(e, "RTP handler failed");
        }
    }

    public Task SetRemoteOfferAsync(string sdp)
    {
        var parsed = Sdp.Parse(sdp);
        var audio = parsed.Sections.Where(s => s.Media == "audio").ToList();
        var video = parsed.Sections.Where(s => s.Media == "video").ToList();
        while (_pc.AudioStreamList.Count < audio.Count)
        {
            _pc.addTrack(ReceiveAudioTrack());
        }

        while (_pc.VideoStreamList.Count < video.Count)
        {
            _pc.addTrack(ReceiveVideoTrack());
        }

        var result = _pc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdp });
        if (result != SetDescriptionResultEnum.OK)
        {
            throw new InvalidOperationException($"SIPSorcery rejected the offer: {result}");
        }

        static uint MidOf(SdpSection s) => uint.TryParse(s.Mid, NumberStyles.None, CultureInfo.InvariantCulture, out var m) ? m : uint.MaxValue;
        var codecs = new Dictionary<uint, Dictionary<int, string>>();
        foreach (var section in parsed.Sections)
        {
            codecs[MidOf(section)] = section.Codecs.ToDictionary(c => c.Key, c => $"{section.Media}/{(c.Value == "OPUS" ? "opus" : c.Value)}");
        }

        lock (_lock)
        {
            _offer = sdp;
            _audioMids = audio.Select(MidOf).ToArray();
            _videoMids = video.Select(MidOf).ToArray();
            _codecs = codecs;
        }

        return Task.CompletedTask;
    }

    public Task<string> CreateAnswerAsync()
    {
        string offer;
        lock (_lock)
        {
            offer = _offer;
        }

        var answer = _pc.createAnswer() ?? throw new InvalidOperationException("SIPSorcery created no answer");
        return Task.FromResult(Sdp.NormalizeAnswer(offer, answer.sdp, _config.Camera));
    }

    public async Task SetLocalAnswerAsync(string sdp)
    {
        await _pc.setLocalDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp }).ConfigureAwait(false);
        var receiving = new HashSet<uint>();
        foreach (var section in Sdp.Parse(sdp).Sections)
        {
            if (section.Direction is "recvonly" or "sendrecv" && uint.TryParse(section.Mid, NumberStyles.None, CultureInfo.InvariantCulture, out var mid) && mid >= Sdp.RemoteMidBase)
            {
                receiving.Add(mid);
            }
        }

        lock (_lock)
        {
            _localSdp = sdp;
            _receiving = receiving;
        }
    }

    public Task RollbackAsync() => Task.CompletedTask;

    /// <summary>The id the microphone writes the audio-level extension with (SIPSorcery adopts the offered id when answering).</summary>
    internal int? MicrophoneAudioLevelId =>
        _pc.AudioStreamList[0].LocalTrack?.HeaderExtensions.Values.FirstOrDefault(e => e.Uri == AudioLevelExtension.RTP_HEADER_EXTENSION_URI)?.Id;

    public string LocalSdp
    {
        get
        {
            lock (_lock)
            {
                return _localSdp;
            }
        }
    }

    public IReadOnlyCollection<uint> ReceivingMids()
    {
        lock (_lock)
        {
            return _receiving.ToList();
        }
    }

    public (IAudioUplink? Microphone, IVideoUplink? Camera) Uplinks()
    {
        string offer;
        lock (_lock)
        {
            offer = _offer;
        }

        var parsed = Sdp.Parse(offer);
        var microphoneOffered = parsed.Sections.Any(s => s.Mid == "0" && s.Direction != "inactive");
        var cameraOffered = parsed.Sections.Any(s => s.Mid == "1" && s.Direction != "inactive");
        return (microphoneOffered ? _microphone : null, cameraOffered ? _camera : null);
    }

    public Task CloseAsync()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 0)
        {
            return Task.Run(() =>
            {
                try
                {
                    _pc.close();
                }
                catch (Exception e)
                {
                    _config.Logger.LogDebug(e, "closing the PeerConnection failed");
                }

                _pc.Dispose();
            });
        }

        return Task.CompletedTask;
    }

    private sealed class MicrophoneUplink : IAudioUplink
    {
        private readonly AudioStream _stream;
        private readonly object _lock = new();

        public MicrophoneUplink(AudioStream stream) => _stream = stream;

        public void SendOpus(byte[] frame, uint timestamp, bool marker, byte level, bool voice)
        {
            lock (_lock)
            {
                _stream.SetRtpHeaderExtensionValue(AudioLevelExtension.RTP_HEADER_EXTENSION_URI, new AudioLevelExtension.AudioLevel { Level = level, Voice = voice });
                _stream.SendRtpRaw(frame, timestamp, marker ? 1 : 0, OpusPt);
            }
        }
    }

    private sealed class CameraUplink : IVideoUplink
    {
        private readonly VideoStream _stream;
        private readonly object _lock = new();

        public CameraUplink(VideoStream stream) => _stream = stream;

        public void SendVp8(byte[] frame, uint durationRtpUnits)
        {
            lock (_lock)
            {
                _stream.SendVideo(durationRtpUnits, frame);
            }
        }
    }
}
