using System.Text.Json;

namespace Mezon.Net.Sdk.Example;

internal static class MessageContent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Mezon message bodies are commonly JSON objects with a <c>t</c> text field.
    /// Falls back to the raw content when the payload is not JSON.
    /// </summary>
    public static string ExtractText(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var trimmed = content.Trim();
        if (trimmed.Length == 0 || trimmed[0] != '{')
        {
            return trimmed;
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("t", out var text)
                && text.ValueKind == JsonValueKind.String)
            {
                return text.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // Keep raw content for non-JSON payloads.
        }

        return trimmed;
    }

    public static string BuildTextPayload(string text)
        => JsonSerializer.Serialize(new TextPayload(text), JsonOptions);

    private sealed record TextPayload(string t);
}
