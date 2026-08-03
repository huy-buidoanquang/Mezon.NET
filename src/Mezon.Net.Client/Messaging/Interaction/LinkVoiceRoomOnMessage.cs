namespace Mezon.Net.Client
{
    /// <summary>Voice-room link span token (<c>vk</c>) — UTF-16 start/end into <c>t</c>.</summary>
    public readonly record struct LinkVoiceRoomOnMessage(int? Start, int? End);
}
