namespace Mezon.Net.Client;

/// <summary>YouTube link span token (<c>lky</c>).</summary>
public readonly record struct LinkYoutubeOnMessage(
    int? Start = null,
    int? End = null);
