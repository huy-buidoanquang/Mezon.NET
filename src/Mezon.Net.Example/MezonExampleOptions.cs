namespace Mezon.Net.Example;

public sealed class MezonExampleOptions
{
    public const string SectionName = "Mezon";

    public const long DefaultClanId = 2042062935735406592;

    public const long DefaultChannelId = 2042062936049979392;

    /// <summary>
    /// Example mode: Verify (default), AllApis, ListChannelDescs, etc. Overridden by MEZON_DIAG env var.
    /// </summary>
    public string Mode { get; set; } = "Verify";

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Host { get; set; } = "dev-mezon.nccsoft.vn";

    public string Port { get; set; } = "8088";

    public bool UseSSL { get; set; } = true;

    public string TransportType { get; set; } = "Tcp";

    public bool CreateStatusOnConnect { get; set; } = true;

    public long ClanId { get; set; } = DefaultClanId;

    public long ChannelId { get; set; } = DefaultChannelId;

    public int RunSeconds { get; set; } = 20;

    public int ClanListLimit { get; set; } = 20;

    public int ApiTimeoutMs { get; set; } = 10_000;

    /// <summary>Delay between socket API calls in probe/diagnostic (rate limit mitigation).</summary>
    public int ApiDelayMs { get; set; } = 1_000;

    /// <summary>Timeout used only for ListChannelDescs diagnostic cases.</summary>
    public int ListChannelDescsTimeoutMs { get; set; } = 30_000;

    /// <summary>Cooldown between burst and retry in ListChannelDescs diagnostic.</summary>
    public int CooldownSeconds { get; set; } = 3;

    /// <summary>Run probe stages 1..N only (0 = all stages).</summary>
    public int ProbeMaxStage { get; set; }

    /// <summary>Pause between probe stages.</summary>
    public int StagePauseMs { get; set; } = 2_000;

    public bool RunDestructiveWrites { get; set; }

    /// <summary>Mezon client log level for wire trace.</summary>
    public string SocketLogLevel { get; set; } = "Information";

    /// <summary>When true, skip post-probe heartbeat observe loop.</summary>
    public bool ProbeOnly { get; set; } = false;

    public string TestMessage { get; set; } = "[Mezon.Net Example] socket write test";
}
