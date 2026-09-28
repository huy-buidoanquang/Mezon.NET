using System.Collections.Generic;
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
        int? maxOptions = null,
        GridMessageComponent? shape = null,
        IReadOnlyList<MessageComponent>? buttons = null,
        IReadOnlyDictionary<string, JsonElement>? extensions = null,
        MessageComponent? input = null)
    {
        Name = name;
        Value = value;
        Inline = inline;
        Inputs = inputs;
        Options = options;
        MaxOptions = maxOptions;
        Shape = shape;
        Input = input;
        Buttons = buttons is null || buttons.Count == 0
            ? null
            : new List<MessageComponent>(buttons).ToArray();
        Extensions = extensions is null || extensions.Count == 0
            ? null
            : new Dictionary<string, JsonElement>(extensions, System.StringComparer.Ordinal);
    }

    public string Name { get; }
    public string Value { get; }
    public bool Inline { get; }
    public JsonElement? Inputs { get; }
    public JsonElement? Options { get; }
    public int? MaxOptions { get; }
    /// <summary>Typed embed-field input component serialized under wire property <c>inputs</c>.</summary>
    public MessageComponent? Input { get; }
    public GridMessageComponent? Shape { get; }
    public IReadOnlyList<MessageComponent>? Buttons { get; }
    public IReadOnlyDictionary<string, JsonElement>? Extensions { get; }
}
