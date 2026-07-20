namespace Mezon.Net.Client
{
    /// <summary>Grid cell (<c>IMessageGridItem</c>).</summary>
    public readonly record struct MessageGridItem(
        int? Width = null,
        int? Height = null,
        int? StartCol = null,
        int? StartRow = null);
}
