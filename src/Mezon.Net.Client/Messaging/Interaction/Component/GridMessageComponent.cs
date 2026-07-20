using System.Collections.Generic;

namespace Mezon.Net.Client
{
    /// <summary>
    /// Grid component (<c>GridComponent</c>). <see cref="Columns"/> / <see cref="Rows"/> live on the
    /// envelope (<c>IEmbedShapeComponent</c>), not inside <c>component</c>.
    /// </summary>
    public sealed class GridMessageComponent : MessageComponent
    {
        public GridMessageComponent(
            string id,
            IReadOnlyList<MessageGridItem> items,
            int columns,
            int rows,
            string? urlImage = null,
            string? urlPosition = null)
            : base(id, MessageComponentType.Grid)
        {
            Items = items;
            Columns = columns;
            Rows = rows;
            UrlImage = urlImage;
            UrlPosition = urlPosition;
        }

        public IReadOnlyList<MessageGridItem> Items { get; }
        public int Columns { get; }
        public int Rows { get; }
        public string? UrlImage { get; }
        public string? UrlPosition { get; }
    }
}
