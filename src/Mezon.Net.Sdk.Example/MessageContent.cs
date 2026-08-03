using Mezon.Net.Client;

namespace Mezon.Net.Sdk.Example;

internal static class ExampleMessageContent
{
    public static string ExtractText(string? content)
        => string.IsNullOrWhiteSpace(content)
            ? string.Empty
            : MessageContent.Parse(content).Text ?? string.Empty;

    public static string BuildTextPayload(string text)
        => MessageContent.CreateText(text).ToJson();
}
