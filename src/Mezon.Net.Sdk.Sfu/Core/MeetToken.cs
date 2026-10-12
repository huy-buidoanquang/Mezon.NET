using System;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Mezon.Net.Sdk.Sfu.Core;

/// <summary>Claims of a meet token as the SFU reads them. Nothing here is verified.</summary>
/// <param name="UserId"><c>identity</c> or <c>sub</c>, as a decimal string.</param>
/// <param name="RoomId"><c>room</c> (or LiveKit-style <c>video.room</c>), as a decimal string.</param>
/// <param name="ExpiresAt"><c>exp</c>; the SFU rejects tokens without it.</param>
public sealed record MeetTokenClaims(string? UserId, string? RoomId, DateTimeOffset? ExpiresAt, DateTimeOffset? NotBefore, string? Metadata);

/// <summary>Reads meet tokens (HS256 JWTs issued by the Mezon API) without verifying them.</summary>
public static class MeetToken
{
    /// <summary>
    /// The SFU finds each claim by the first occurrence of its key anywhere in the payload
    /// (<c>mezon-sfu/src/protocol/signaling/handshake.c</c>), so <c>room</c> also matches a nested
    /// <c>video.room</c>. Ids are read from the raw text so 64-bit values keep every digit.
    /// </summary>
    private static string? FirstInteger(string payload, string key)
    {
        var needle = $"\"{key}\"";
        var from = 0;
        while (true)
        {
            var at = payload.IndexOf(needle, from, StringComparison.Ordinal);
            if (at < 0)
            {
                return null;
            }

            from = at + needle.Length;
            var rest = payload.Substring(from).TrimStart();
            if (!rest.StartsWith(":", StringComparison.Ordinal))
            {
                continue;
            }

            var value = rest.Substring(1).TrimStart();
            if (value.StartsWith("\"", StringComparison.Ordinal))
            {
                value = value.Substring(1);
            }

            var digits = new StringBuilder();
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if ((c >= '0' && c <= '9') || (i == 0 && c == '-'))
                {
                    digits.Append(c);
                }
                else
                {
                    break;
                }
            }

            var text = digits.ToString();
            if (text.Length == 0 || text == "-")
            {
                return null;
            }

            var negative = text.StartsWith("-", StringComparison.Ordinal);
            var normalized = text.TrimStart('-').TrimStart('0');
            var sign = negative && normalized.Length > 0 ? "-" : "";
            return sign + (normalized.Length == 0 ? "0" : normalized);
        }
    }

    private static DateTimeOffset? Seconds(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var secs))
        {
            return null;
        }

        // Times the platform cannot represent (beyond year 9999) count as missing.
        if (secs < 0 || secs > DateTimeOffset.MaxValue.ToUnixTimeSeconds() || double.IsNaN(secs))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds((long)secs);
    }

    /// <summary>
    /// Reads the claims of a meet token without verifying its signature (the SDK never holds the secret). Null
    /// when <paramref name="token"/> is not a three-part JWT with a JSON object payload.
    /// </summary>
    public static MeetTokenClaims? Decode(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3 || parts[1].Length == 0)
        {
            return null;
        }

        string text;
        try
        {
            text = Encoding.UTF8.GetString(Base64UrlDecode(parts[1].TrimEnd('=')));
        }
        catch (FormatException)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new MeetTokenClaims(
                FirstInteger(text, "identity") ?? FirstInteger(text, "sub"),
                FirstInteger(text, "room"),
                Seconds(root, "exp"),
                Seconds(root, "nbf"),
                root.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.String ? metadata.GetString() : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether <paramref name="token"/> expires within <paramref name="margin"/> of <paramref name="now"/>. Tokens
    /// without a readable <c>exp</c> count as expiring, because the SFU rejects them.
    /// </summary>
    public static bool NeedsRefresh(string token, DateTimeOffset now, TimeSpan margin)
    {
        var expires = Decode(token)?.ExpiresAt;
        if (expires is null)
        {
            return true;
        }

        return expires.Value - now <= margin;
    }

    private static byte[] Base64UrlDecode(string text)
    {
        if (text.Any(c => !(char.IsLetterOrDigit(c) && c < 128) && c != '-' && c != '_'))
        {
            throw new FormatException("not base64url");
        }

        var s = text.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2:
                s += "==";
                break;
            case 3:
                s += "=";
                break;
            case 1:
                throw new FormatException("bad base64url length");
        }

        return Convert.FromBase64String(s);
    }
}

/// <summary>Display information carried in a participant's metadata.</summary>
public sealed record ParticipantInfo(string? DisplayName, string? AvatarUrl);

/// <summary>Participant metadata, conventionally <c>"display_name;avatar_url"</c>.</summary>
public static class ParticipantMetadata
{
    private static string? NonEmpty(string? s)
    {
        var t = s?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    /// <summary>
    /// Parses participant metadata. Mezon issuers write <c>"display_name;avatar_url"</c>; the avatar part may be
    /// empty. A JSON object with <c>username</c>/<c>avatar</c> (written by some older clients) is also understood.
    /// Anything else becomes the display name.
    /// </summary>
    public static ParticipantInfo Parse(string metadata)
    {
        var text = metadata.Trim();
        if (text.Length == 0)
        {
            return new ParticipantInfo(null, null);
        }

        if (text.StartsWith("{", StringComparison.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                var o = doc.RootElement;
                if (o.ValueKind == JsonValueKind.Object)
                {
                    string? Get(string key) => o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? NonEmpty(v.GetString()) : null;
                    return new ParticipantInfo(Get("username") ?? Get("display_name") ?? Get("name"), Get("avatar"));
                }
            }
            catch (JsonException)
            {
            }
        }

        var split = text.IndexOf(';');
        return split < 0
            ? new ParticipantInfo(NonEmpty(text), null)
            : new ParticipantInfo(NonEmpty(text.Substring(0, split)), NonEmpty(text.Substring(split + 1)));
    }

    /// <summary>Formats <c>"display_name;avatar_url"</c>. Semicolons in the display name become commas.</summary>
    public static string Format(ParticipantInfo info) => $"{(info.DisplayName ?? "").Replace(';', ',')};{info.AvatarUrl ?? ""}";
}
