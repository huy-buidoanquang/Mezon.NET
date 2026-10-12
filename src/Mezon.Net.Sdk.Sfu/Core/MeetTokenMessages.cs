using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Mezon.Net.Sdk.Sfu.Core;

/// <summary>Request body of <c>GenerateMeetToken</c>. Ids are decimal int64 strings.</summary>
/// <param name="Metadata">Becomes the participant metadata, conventionally <c>"display_name;avatar_url"</c>.</param>
public sealed record GenerateMeetTokenRequest(string ChannelId, string RoomName = "", string Metadata = "");

/// <summary>Request body of the mute/kick participant APIs.</summary>
public sealed record MeetParticipantRequest(string UserId, string ChannelId, string ClanId);

/// <param name="Url">The SFU WebSocket URL configured on the server, when it returns one.</param>
public sealed record GenerateMeetTokenResponse(string Token, string? Url);

public sealed record GenerateMeetTokenExternalResponse(string Token, string? GuestUserId, string? GuestAccessToken, string? Url);

/// <summary>A protobuf message that cannot be encoded or decoded.</summary>
public sealed class MeetTokenProtobufException : Exception
{
    public MeetTokenProtobufException(string message)
        : base($"meet token protobuf: {message}")
    {
    }
}

/// <summary>
/// Mezon API messages for obtaining SFU meet tokens, from <c>mezon-protocol/api/api.proto</c> (package
/// <c>mezon.api</c>). Encoded without a protobuf runtime: these messages only use int64 and string fields. A
/// unit test checks the field numbers against the schema.
/// </summary>
public static class MeetTokenMessages
{
    /// <summary>Message → fields (name, number, scalar type), as in <c>api.proto</c>.</summary>
    public static IReadOnlyList<(string Message, IReadOnlyList<(string Name, int Number, string Type)> Fields)> Schema { get; } = new (string, IReadOnlyList<(string, int, string)>)[]
    {
        ("GenerateMeetTokenRequest", new[] { ("channel_id", 1, "int64"), ("room_name", 2, "string"), ("metadata", 3, "string") }),
        ("GenerateMeetTokenResponse", new[] { ("token", 1, "string"), ("url", 2, "string") }),
        ("MeetParticipantRequest", new[] { ("user_id", 1, "int64"), ("channel_id", 2, "int64"), ("clan_id", 3, "int64") }),
        ("GenerateMeetTokenExternalResponse", new[] { ("token", 1, "string"), ("guest_user_id", 2, "int64"), ("guest_access_token", 3, "string"), ("url", 4, "string") }),
    };

    private static void Varint(ulong value, Stream output)
    {
        while (value >= 0x80)
        {
            output.WriteByte((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        output.WriteByte((byte)value);
    }

    private static void Int64Field(int field, string decimalText, Stream output)
    {
        if (decimalText.Length == 0 || decimalText == "0")
        {
            return;
        }

        if (!long.TryParse(decimalText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n))
        {
            throw new MeetTokenProtobufException($"\"{decimalText}\" is not an int64");
        }

        Varint((ulong)(field << 3), output);
        Varint(unchecked((ulong)n), output);
    }

    private static void StringField(int field, string text, Stream output)
    {
        if (text.Length == 0)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        Varint((ulong)((field << 3) | 2), output);
        Varint((ulong)bytes.Length, output);
        output.Write(bytes, 0, bytes.Length);
    }

    /// <summary>Protobuf encoding of <c>mezon.api.GenerateMeetTokenRequest</c>.</summary>
    public static byte[] Encode(GenerateMeetTokenRequest request)
    {
        using var output = new MemoryStream();
        Int64Field(1, request.ChannelId, output);
        StringField(2, request.RoomName, output);
        StringField(3, request.Metadata, output);
        return output.ToArray();
    }

    /// <summary>Protobuf encoding of <c>mezon.api.MeetParticipantRequest</c>.</summary>
    public static byte[] Encode(MeetParticipantRequest request)
    {
        using var output = new MemoryStream();
        Int64Field(1, request.UserId, output);
        Int64Field(2, request.ChannelId, output);
        Int64Field(3, request.ClanId, output);
        return output.ToArray();
    }

    private static ulong ReadVarint(ReadOnlySpan<byte> data, ref int pos)
    {
        ulong result = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            if (pos >= data.Length)
            {
                throw new MeetTokenProtobufException("truncated varint");
            }

            var b = data[pos++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return result;
            }
        }

        throw new MeetTokenProtobufException("varint too long");
    }

    private readonly record struct Field(int Number, ulong Varint, byte[]? Bytes);

    private static List<Field> ReadFields(ReadOnlySpan<byte> data)
    {
        var fields = new List<Field>();
        var pos = 0;
        while (pos < data.Length)
        {
            var key = ReadVarint(data, ref pos);
            var number = (int)(key >> 3);
            if (number == 0)
            {
                throw new MeetTokenProtobufException("field number 0");
            }

            switch (key & 7)
            {
                case 0:
                    fields.Add(new Field(number, ReadVarint(data, ref pos), null));
                    break;
                case 2:
                    var len = ReadVarint(data, ref pos);
                    if (len > (ulong)(data.Length - pos))
                    {
                        throw new MeetTokenProtobufException("truncated field");
                    }

                    fields.Add(new Field(number, 0, data.Slice(pos, (int)len).ToArray()));
                    pos += (int)len;
                    break;
                case 1:
                    pos += 8;
                    break;
                case 5:
                    pos += 4;
                    break;
                default:
                    throw new MeetTokenProtobufException($"unsupported wire type {key & 7}");
            }

            if (pos > data.Length)
            {
                throw new MeetTokenProtobufException("truncated field");
            }
        }

        return fields;
    }

    private static string? Text(List<Field> fields, int number)
    {
        for (var i = fields.Count - 1; i >= 0; i--)
        {
            if (fields[i].Number != number)
            {
                continue;
            }

            if (fields[i].Bytes is not { } bytes)
            {
                return null;
            }

            try
            {
                var s = new UTF8Encoding(false, true).GetString(bytes);
                return s.Length == 0 ? null : s;
            }
            catch (DecoderFallbackException)
            {
                throw new MeetTokenProtobufException($"field {number} is not UTF-8");
            }
        }

        return null;
    }

    private static bool LooksLikeJwt(string s)
    {
        var parts = s.Split('.');
        static bool Ok(string p) => p.All(c => (c < 128 && char.IsLetterOrDigit(c)) || c == '-' || c == '_');
        return parts.Length == 3 && parts[0].Length > 0 && parts[1].Length > 0 && parts.All(Ok);
    }

    /// <summary>
    /// Decodes the raw bytes returned by <c>GenerateMeetToken</c> (socket API) or the participant APIs. Servers
    /// return either a protobuf <c>GenerateMeetTokenResponse</c> or, for older builds and moderation tokens, the
    /// bare JWT text; both are accepted.
    /// </summary>
    public static GenerateMeetTokenResponse DecodeGenerateMeetTokenResponse(ReadOnlySpan<byte> data)
    {
        try
        {
            var raw = new UTF8Encoding(false, true).GetString(data.ToArray()).Trim();
            if (LooksLikeJwt(raw))
            {
                return new GenerateMeetTokenResponse(raw, null);
            }
        }
        catch (DecoderFallbackException)
        {
        }

        var fields = ReadFields(data);
        var token = Text(fields, 1) ?? throw new MeetTokenProtobufException("response has no token");
        return new GenerateMeetTokenResponse(token, Text(fields, 2));
    }

    /// <summary>Decodes <c>mezon.api.GenerateMeetTokenExternalResponse</c> (guest/external meetings).</summary>
    public static GenerateMeetTokenExternalResponse DecodeGenerateMeetTokenExternalResponse(ReadOnlySpan<byte> data)
    {
        var fields = ReadFields(data);
        var token = Text(fields, 1) ?? throw new MeetTokenProtobufException("response has no token");
        string? guest = null;
        for (var i = fields.Count - 1; i >= 0; i--)
        {
            if (fields[i].Number == 2 && fields[i].Bytes is null)
            {
                guest = unchecked((long)fields[i].Varint).ToString(CultureInfo.InvariantCulture);
                break;
            }
        }

        return new GenerateMeetTokenExternalResponse(token, guest, Text(fields, 3), Text(fields, 4));
    }
}
