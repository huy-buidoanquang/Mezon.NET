namespace Mezon.Net.Client;

/// <summary>Legacy bold span token (<c>bm</c>).</summary>
public readonly record struct BoldOnMessage(
    string? Language = null,
    int? Start = null,
    int? End = null);
