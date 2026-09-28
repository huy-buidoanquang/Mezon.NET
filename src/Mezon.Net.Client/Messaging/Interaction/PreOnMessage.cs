namespace Mezon.Net.Client;

/// <summary>Legacy preformatted span token (<c>pre</c>).</summary>
public readonly record struct PreOnMessage(
    string? Language = null,
    int? Start = null,
    int? End = null);
