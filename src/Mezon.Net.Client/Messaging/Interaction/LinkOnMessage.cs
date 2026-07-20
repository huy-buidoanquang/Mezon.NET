namespace Mezon.Net.Client
{
    /// <summary>Plain URL span token (<c>lk</c>) — UTF-16 start/end into <c>t</c>.</summary>
    public readonly record struct LinkOnMessage(int? Start, int? End);
}
