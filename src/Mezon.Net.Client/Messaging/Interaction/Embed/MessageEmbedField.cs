using System.Text.Json;

namespace Mezon.Net.Client;

public sealed class MessageEmbedField
{
    public MessageEmbedField(
        string name,
        string value,
        bool inline = false,
        JsonElement? inputs = null,
        JsonElement? options = null,
        int? maxOptions = null)
    {
        Name = name;
        Value = value;
        Inline = inline;
        Inputs = inputs;
        Options = options;
        MaxOptions = maxOptions;
    }

    public string Name { get; }
    public string Value { get; }
    public bool Inline { get; }
    public JsonElement? Inputs { get; }
    public JsonElement? Options { get; }
    public int? MaxOptions { get; }
}
