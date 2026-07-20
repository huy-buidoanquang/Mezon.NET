using System.Collections.Generic;
using System.Text.Json;

namespace Mezon.Net.Client
{
    /// <summary>
    /// Markdown / backtick token (<c>mk</c>).
    /// </summary>
    /// <remarks>
    /// <see cref="Type"/> is the span kind (<see cref="MarkdownMarkerType"/>): bold, code, pre,
    /// link variants, etc. Offsets are UTF-16 indices into content text <c>t</c>.
    /// </remarks>
    public readonly record struct MarkdownOnMessage(
        string? Type,
        int? Start,
        int? End,
        string? Url = null,
        string? Language = null,
        IReadOnlyDictionary<string, JsonElement>? Extensions = null);
}
