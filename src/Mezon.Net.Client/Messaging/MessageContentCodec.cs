using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Mezon.Net.Client
{
    /// <summary>
    /// Encode / decode helpers for <see cref="MessageContent"/> wire JSON.
    /// Prefer the public <see cref="MessageContent"/> API from call sites.
    /// </summary>
    internal static class MessageContentCodec
    {
        private static bool IsKnownRootProperty(string name) =>
            name is "t" or "e2ee" or "hg" or "ej" or "lk" or "mk" or "pre" or "bm" or "vk" or "lky"
                or "embed" or "components" or "canvas" or "cvtt" or "callLog" or "tp" or "cid" or "fwd"
                or "isCard" or "rpl" or "lsnt" or "question" or "question_emoji_id" or "answers"
                or "answer_emoji_ids" or "answer_counts" or "expire_time" or "is_closed" or "total_votes"
                or "allow_multiple_answers" or "user_votes" or "id" or "expire_at" or "type"
                or "presign_finish" or "create_time_seconds";

        internal static string NormalizeRawJson(string? rawJson)
        {
            if (rawJson is null)
            {
                return WriteTextPayload(string.Empty);
            }

            var trimmed = rawJson.Trim();
            if (trimmed.Length == 0 || trimmed == "[]")
            {
                return WriteTextPayload(trimmed);
            }

            if (IsValidJsonObject(trimmed))
            {
                return trimmed;
            }

            // Only allocate a fixed copy when CR/LF are present (common malformed paste case).
            if (trimmed.IndexOf('\n') >= 0 || trimmed.IndexOf('\r') >= 0)
            {
                var fixedJson = trimmed.Replace("\n", "\\n").Replace("\r", "\\r");
                if (IsValidJsonObject(fixedJson))
                {
                    return fixedJson;
                }
            }

            return WriteTextPayload(trimmed);
        }

        /// <summary>Validates a single JSON object without allocating <see cref="JsonDocument"/>.</summary>
        internal static bool IsValidJsonObject(string json)
        {
            if (json.Length == 0 || json[0] != '{')
            {
                return false;
            }

            if (!TryRentUtf8(json, out var rented, out var written))
            {
                return false;
            }

            try
            {
                var reader = new Utf8JsonReader(rented.AsSpan(0, written));
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                {
                    return false;
                }

                reader.Skip();
                return !reader.Read();
            }
            catch (JsonException)
            {
                return false;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        /// <summary>Reads root <c>t</c> without materializing the full typed snapshot.</summary>
        internal static string? TryReadTextProperty(string json)
        {
            if (json.Length == 0 || json[0] != '{')
            {
                return null;
            }

            if (!TryRentUtf8(json, out var rented, out var written))
            {
                return null;
            }

            try
            {
                var reader = new Utf8JsonReader(rented.AsSpan(0, written));
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                {
                    return null;
                }

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        break;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        continue;
                    }

                    if (reader.ValueTextEquals("t"))
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                    }

                    if (!reader.Read())
                    {
                        return null;
                    }

                    reader.Skip();
                }

                return null;
            }
            catch (JsonException)
            {
                return null;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        private static bool TryRentUtf8(string json, out byte[] rented, out int written)
        {
            var byteCount = Encoding.UTF8.GetByteCount(json);
            rented = ArrayPool<byte>.Shared.Rent(byteCount);
            written = Encoding.UTF8.GetBytes(json, 0, json.Length, rented, 0);
            return true;
        }

        internal static bool TryParseJsonObject(string json, out JsonDocument document)
        {
            document = null!;
            if (json.Length == 0 || json[0] != '{')
            {
                return false;
            }

            try
            {
                document = JsonDocument.Parse(json);
                return document.RootElement.ValueKind == JsonValueKind.Object;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        internal static MessageContentSnapshot ParseSnapshot(string rawJson)
        {
            if (!TryParseJsonObject(rawJson, out var document))
            {
                return new MessageContentSnapshot(
                    text: null,
                    hashtags: null,
                    emojis: null,
                    links: null,
                    markdown: null,
                    voiceLinks: null,
                    embeds: null,
                    components: null,
                    unknown: null);
            }

            using (document)
            {
                var root = document.RootElement;
                var text = ReadOptionalString(root, "t");

                var hashtags = ReadArray(root, "hg", ReadHashtag);
                var emojis = ReadArray(root, "ej", ReadEmoji);
                var links = ReadArray(root, "lk", ReadLink);
                var markdown = ReadArray(root, "mk", ReadMarkdown);
                var pre = ReadArray(root, "pre", ReadPre);
                var bold = ReadArray(root, "bm", ReadBold);
                var voiceLinks = ReadArray(root, "vk", ReadVoiceLink);
                var youtubeLinks = ReadArray(root, "lky", ReadYoutubeLink);
                var embeds = ReadArray(root, "embed", ReadEmbed);
                var components = ReadComponents(root);
                var poll = ReadPoll(root);
                var canvas = ReadJsonValue(root, "canvas");
                var canvasTitles = ReadStringMap(root, "cvtt");
                var callLog = ReadCallLog(root);

                Dictionary<string, JsonElement>? unknown = null;
                foreach (var property in root.EnumerateObject())
                {
                    if (IsKnownRootProperty(property.Name))
                    {
                        continue;
                    }

                    unknown ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                    unknown[property.Name] = property.Value.Clone();
                }

                ValidateOffsets(text, hashtags, emojis, links, markdown, pre, bold, voiceLinks, youtubeLinks);

                return new MessageContentSnapshot(
                    text,
                    hashtags,
                    emojis,
                    links,
                    markdown,
                    voiceLinks,
                    embeds,
                    components,
                    unknown,
                    pre,
                    bold,
                    youtubeLinks,
                    ReadOptionalInt(root, "e2ee"),
                    canvas,
                    canvasTitles,
                    callLog,
                    ReadStringProperty(root, "tp"),
                    ReadStringProperty(root, "cid"),
                    ReadOptionalBool(root, "fwd"),
                    ReadOptionalBool(root, "isCard"),
                    ReadOptionalLong(root, "rpl"),
                    ReadOptionalLong(root, "lsnt"),
                    poll,
                    ReadStringArray(root, "presign_finish"),
                    ReadOptionalLong(root, "create_time_seconds"));
            }
        }

        internal static string WriteText(string text)
        {
            var json = WriteTextPayload(text);
            return json;
        }

        internal static string Serialize(in MessageContentSnapshot snapshot)
        {
            using var stream = new MemoryStream(256);
            using (var writer = new Utf8JsonWriter(stream))
            {
                WriteSnapshot(writer, snapshot);
            }

            var json = Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
            return json;
        }

        internal static string WriteTextPayload(string text)
        {
            var capacity = Encoding.UTF8.GetMaxByteCount(text.Length) + 16;
            using var stream = new MemoryStream(capacity);
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("t", text);
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
        }

        private static void WriteSnapshot(Utf8JsonWriter writer, in MessageContentSnapshot snapshot)
        {
            writer.WriteStartObject();

            if (snapshot.Text is not null)
            {
                writer.WriteString("t", snapshot.Text);
            }

            WriteHashtagArray(writer, "hg", snapshot.Hashtags);
            WriteOptionalInt(writer, "e2ee", snapshot.E2ee);
            WriteEmojiArray(writer, "ej", snapshot.Emojis);
            WriteLinkArray(writer, "lk", snapshot.Links);
            WriteMarkdownArray(writer, "mk", snapshot.Markdown);
            WritePreArray(writer, "pre", snapshot.Pre);
            WriteBoldArray(writer, "bm", snapshot.Bold);
            WriteVoiceLinkArray(writer, "vk", snapshot.VoiceLinks);
            WriteYoutubeLinkArray(writer, "lky", snapshot.YoutubeLinks);
            WriteJsonValue(writer, "canvas", snapshot.Canvas);
            WriteStringMap(writer, "cvtt", snapshot.CanvasTitles);
            WriteCallLog(writer, snapshot.CallLog);
            WriteOptionalString(writer, "tp", snapshot.Type);
            WriteOptionalString(writer, "cid", snapshot.ChannelId);
            WriteOptionalBool(writer, "fwd", snapshot.Forwarded);
            WriteOptionalBool(writer, "isCard", snapshot.IsCard);
            WriteOptionalLong(writer, "rpl", snapshot.ReplyToMessageId);
            WriteOptionalLong(writer, "lsnt", snapshot.LastSeenSeconds);
            WritePoll(writer, snapshot.Poll);
            WriteStringArray(writer, "presign_finish", snapshot.PresignFinish);
            WriteOptionalLong(writer, "create_time_seconds", snapshot.CreateTimeSeconds);
            WriteEmbedArray(writer, snapshot.Embeds);
            WriteComponents(writer, snapshot.Components);

            if (snapshot.Unknown is not null)
            {
                foreach (var pair in snapshot.Unknown)
                {
                    if (IsKnownRootProperty(pair.Key))
                    {
                        throw new InvalidOperationException($"Message extension '{pair.Key}' duplicates a typed root property.");
                    }

                    writer.WritePropertyName(pair.Key);
                    pair.Value.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        private static void WriteHashtagArray(Utf8JsonWriter writer, string propertyName, IReadOnlyList<HashtagOnMessage>? values)
        {
            if (values is null || values.Count == 0)
            {
                return;
            }

            writer.WriteStartArray(propertyName);
            foreach (var value in values)
            {
                writer.WriteStartObject();
                if (value.ChannelId is not null)
                {
                    writer.WriteString("channelId", value.ChannelId);
                }

                WriteOptionalInt(writer, "s", value.Start);
                WriteOptionalInt(writer, "e", value.End);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WritePoll(Utf8JsonWriter writer, MessagePoll? poll)
        {
            if (poll is null)
            {
                return;
            }

            WriteOptionalString(writer, "question", poll.Question);
            WriteOptionalString(writer, "question_emoji_id", poll.QuestionEmojiId);
            if (poll.Answers is not null)
            {
                writer.WriteStartArray("answers");
                foreach (var answer in poll.Answers)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("index", answer.Index);
                    writer.WriteString("label", answer.Label);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            WriteStringArray(writer, "answer_emoji_ids", poll.AnswerEmojiIds);
            WriteIntArray(writer, "answer_counts", poll.AnswerCounts);
            WriteOptionalLong(writer, "expire_time", poll.ExpireTime);
            WriteOptionalBool(writer, "is_closed", poll.IsClosed);
            WriteOptionalInt(writer, "total_votes", poll.TotalVotes);
            WriteOptionalBool(writer, "allow_multiple_answers", poll.AllowMultipleAnswers);
            WriteIntArray(writer, "user_votes", poll.UserVotes);
            WriteOptionalLong(writer, "id", poll.Id);
            WriteOptionalLong(writer, "expire_at", poll.ExpireAt);
            WriteOptionalInt(writer, "type", poll.Type);
        }

        private static void WriteCallLog(Utf8JsonWriter writer, MessageCallLog? callLog)
        {
            if (callLog is not MessageCallLog value)
            {
                return;
            }

            writer.WriteStartObject("callLog");
            writer.WriteBoolean("isVideo", value.IsVideo);
            writer.WriteNumber("callLogType", value.CallLogType);
            WriteOptionalBool(writer, "showCallBack", value.ShowCallBack);
            writer.WriteEndObject();
        }

        private static void WriteStringArray(Utf8JsonWriter writer, string propertyName, IReadOnlyList<string>? values)
        {
            if (values is null || values.Count == 0)
            {
                return;
            }

            writer.WriteStartArray(propertyName);
            foreach (var value in values)
            {
                writer.WriteStringValue(value);
            }

            writer.WriteEndArray();
        }

        private static void WriteIntArray(Utf8JsonWriter writer, string propertyName, IReadOnlyList<int>? values)
        {
            if (values is null || values.Count == 0)
            {
                return;
            }

            writer.WriteStartArray(propertyName);
            foreach (var value in values)
            {
                writer.WriteNumberValue(value);
            }

            writer.WriteEndArray();
        }

        private static void WriteStringMap(
            Utf8JsonWriter writer,
            string propertyName,
            IReadOnlyDictionary<string, string>? values)
        {
            if (values is null || values.Count == 0)
            {
                return;
            }

            writer.WriteStartObject(propertyName);
            foreach (var pair in values)
            {
                writer.WriteString(pair.Key, pair.Value);
            }

            writer.WriteEndObject();
        }

        private static void WriteJsonValue(Utf8JsonWriter writer, string propertyName, JsonElement? value)
        {
            if (value is not JsonElement element)
            {
                return;
            }

            writer.WritePropertyName(propertyName);
            element.WriteTo(writer);
        }

        private static void WriteOptionalString(Utf8JsonWriter writer, string propertyName, string? value)
        {
            if (value is not null)
            {
                writer.WriteString(propertyName, value);
            }
        }

        private static void WriteOptionalBool(Utf8JsonWriter writer, string propertyName, bool? value)
        {
            if (value is bool flag)
            {
                writer.WriteBoolean(propertyName, flag);
            }
        }

        private static void WriteOptionalLong(Utf8JsonWriter writer, string propertyName, long? value)
        {
            if (value is long number)
            {
                writer.WriteNumber(propertyName, number);
            }
        }

        private static void WriteEmojiArray(Utf8JsonWriter writer, string propertyName, IReadOnlyList<EmojiOnMessage>? values)
        {
            if (values is null || values.Count == 0)
            {
                return;
            }

            writer.WriteStartArray(propertyName);
            foreach (var value in values)
            {
                writer.WriteStartObject();
                if (value.EmojiId is not null)
                {
                    writer.WriteString("emojiid", value.EmojiId);
                }

                WriteOptionalInt(writer, "s", value.Start);
                WriteOptionalInt(writer, "e", value.End);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteLinkArray(Utf8JsonWriter writer, string propertyName, IReadOnlyList<LinkOnMessage>? values)
        {
            if (values is null || values.Count == 0)
            {
                return;
            }

            writer.WriteStartArray(propertyName);
            foreach (var value in values)
            {
                writer.WriteStartObject();
                WriteOptionalInt(writer, "s", value.Start);
                WriteOptionalInt(writer, "e", value.End);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteVoiceLinkArray(Utf8JsonWriter writer, string propertyName, IReadOnlyList<LinkVoiceRoomOnMessage>? values)
        {
            if (values is null || values.Count == 0)
            {
                return;
            }

            writer.WriteStartArray(propertyName);
            foreach (var value in values)
            {
                writer.WriteStartObject();
                WriteOptionalInt(writer, "s", value.Start);
                WriteOptionalInt(writer, "e", value.End);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteMarkdownArray(Utf8JsonWriter writer, string propertyName, IReadOnlyList<MarkdownOnMessage>? values)
        {
            if (values is null || values.Count == 0)
            {
                return;
            }

            writer.WriteStartArray(propertyName);
            foreach (var value in values)
            {
                writer.WriteStartObject();
                if (value.Type is not null)
                {
                    writer.WriteString("type", value.Type);
                }

                WriteOptionalInt(writer, "s", value.Start);
                WriteOptionalInt(writer, "e", value.End);

                if (value.Url is not null)
                {
                    writer.WriteString("url", value.Url);
                }

                if (value.Language is not null)
                {
                    writer.WriteString("language", value.Language);
                }

                if (value.Extensions is not null)
                {
                    foreach (var pair in value.Extensions)
                    {
                        if (IsKnownMarkdownProperty(pair.Key))
                        {
                            throw new InvalidOperationException($"Markdown extension '{pair.Key}' duplicates a typed property.");
                        }

                        writer.WritePropertyName(pair.Key);
                        pair.Value.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WritePreArray(Utf8JsonWriter writer, string propertyName, IReadOnlyList<PreOnMessage>? values)
        {
            if (values is null || values.Count == 0)
            {
                return;
            }

            writer.WriteStartArray(propertyName);
            foreach (var value in values)
            {
                writer.WriteStartObject();
                if (value.Language is not null)
                {
                    writer.WriteString("l", value.Language);
                }

                WriteOptionalInt(writer, "s", value.Start);
                WriteOptionalInt(writer, "e", value.End);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteBoldArray(Utf8JsonWriter writer, string propertyName, IReadOnlyList<BoldOnMessage>? values)
        {
            if (values is null || values.Count == 0)
            {
                return;
            }

            writer.WriteStartArray(propertyName);
            foreach (var value in values)
            {
                writer.WriteStartObject();
                if (value.Language is not null)
                {
                    writer.WriteString("l", value.Language);
                }

                WriteOptionalInt(writer, "s", value.Start);
                WriteOptionalInt(writer, "e", value.End);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteYoutubeLinkArray(Utf8JsonWriter writer, string propertyName, IReadOnlyList<LinkYoutubeOnMessage>? values)
        {
            if (values is null || values.Count == 0)
            {
                return;
            }

            writer.WriteStartArray(propertyName);
            foreach (var value in values)
            {
                writer.WriteStartObject();
                WriteOptionalInt(writer, "s", value.Start);
                WriteOptionalInt(writer, "e", value.End);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteEmbedArray(Utf8JsonWriter writer, IReadOnlyList<MessageEmbed>? embeds)
        {
            if (embeds is null || embeds.Count == 0)
            {
                return;
            }

            writer.WriteStartArray("embed");
            foreach (var embed in embeds)
            {
                WriteEmbed(writer, embed);
            }

            writer.WriteEndArray();
        }

        private static void WriteEmbed(Utf8JsonWriter writer, MessageEmbed embed)
        {
            writer.WriteStartObject();
            if (embed.Color is not null)
            {
                writer.WriteString("color", embed.Color);
            }

            if (embed.Title is not null)
            {
                writer.WriteString("title", embed.Title);
            }

            if (embed.Url is not null)
            {
                writer.WriteString("url", embed.Url);
            }

            if (embed.Author is not null)
            {
                writer.WriteStartObject("author");
                writer.WriteString("name", embed.Author.Name);
                if (embed.Author.IconUrl is not null)
                {
                    writer.WriteString("icon_url", embed.Author.IconUrl);
                }

                if (embed.Author.Url is not null)
                {
                    writer.WriteString("url", embed.Author.Url);
                }

                writer.WriteEndObject();
            }

            if (embed.Description is not null)
            {
                writer.WriteString("description", embed.Description);
            }

            if (embed.Thumbnail is not null)
            {
                writer.WriteStartObject("thumbnail");
                writer.WriteString("url", embed.Thumbnail.Url);
                writer.WriteEndObject();
            }

            if (embed.Fields is not null && embed.Fields.Count > 0)
            {
                writer.WriteStartArray("fields");
                foreach (var field in embed.Fields)
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", field.Name);
                    writer.WriteString("value", field.Value);
                    if (field.Inline)
                    {
                        writer.WriteBoolean("inline", true);
                    }

                    if (field.Inputs is JsonElement inputs)
                    {
                        writer.WritePropertyName("inputs");
                        inputs.WriteTo(writer);
                    }
                    else if (field.Input is not null)
                    {
                        writer.WritePropertyName("inputs");
                        WriteComponent(writer, field.Input);
                    }

                    if (field.Options is JsonElement options)
                    {
                        writer.WritePropertyName("options");
                        options.WriteTo(writer);
                    }

                    if (field.MaxOptions is int maxOptions)
                    {
                        writer.WriteNumber("max_options", maxOptions);
                    }

                    if (field.Shape is not null)
                    {
                        writer.WritePropertyName("shape");
                        WriteComponent(writer, field.Shape);
                    }

                    if (field.Buttons is not null && field.Buttons.Count > 0)
                    {
                        writer.WriteStartArray("button");
                        foreach (var button in field.Buttons)
                        {
                            WriteComponent(writer, button);
                        }

                        writer.WriteEndArray();
                    }

                    if (field.Extensions is not null)
                    {
                        foreach (var pair in field.Extensions)
                        {
                            if (pair.Key is "name" or "value" or "inline" or "inputs" or "options" or "max_options" or "shape" or "button")
                            {
                                throw new InvalidOperationException($"Embed field extension '{pair.Key}' duplicates a typed property.");
                            }

                            writer.WritePropertyName(pair.Key);
                            pair.Value.WriteTo(writer);
                        }
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            if (embed.Image is not null)
            {
                writer.WriteStartObject("image");
                writer.WriteString("url", embed.Image.Url);
                if (embed.Image.Width is not null)
                {
                    writer.WriteString("width", embed.Image.Width);
                }

                if (embed.Image.Height is not null)
                {
                    writer.WriteString("height", embed.Image.Height);
                }

                writer.WriteEndObject();
            }

            if (embed.Timestamp is not null)
            {
                writer.WriteString("timestamp", embed.Timestamp);
            }

            if (embed.Footer is not null)
            {
                writer.WriteStartObject("footer");
                writer.WriteString("text", embed.Footer.Text);
                if (embed.Footer.IconUrl is not null)
                {
                    writer.WriteString("icon_url", embed.Footer.IconUrl);
                }

                writer.WriteEndObject();
            }

            if (embed.Extensions is not null)
            {
                foreach (var pair in embed.Extensions)
                {
                    if (IsKnownEmbedProperty(pair.Key))
                    {
                        throw new InvalidOperationException($"Embed extension '{pair.Key}' duplicates a typed property.");
                    }

                    writer.WritePropertyName(pair.Key);
                    pair.Value.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        internal static string SerializeComponentList(IReadOnlyList<MessageComponent> components)
        {
            using var stream = new MemoryStream(256);
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartArray();
                foreach (var component in components)
                {
                    WriteComponent(writer, component);
                }

                writer.WriteEndArray();
            }

            return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
        }

        private static void WriteComponents(Utf8JsonWriter writer, IReadOnlyList<MessageActionRow>? rows)
        {
            if (rows is null || rows.Count == 0)
            {
                return;
            }

            writer.WriteStartArray("components");
            foreach (var row in rows)
            {
                writer.WriteStartObject();
                writer.WriteStartArray("components");
                foreach (var component in row.Components)
                {
                    WriteComponent(writer, component);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteComponent(Utf8JsonWriter writer, MessageComponent component)
        {
            writer.WriteStartObject();
            writer.WriteString("id", component.Id);
            writer.WriteNumber("type", (int)component.ComponentType);

            switch (component)
            {
                case ButtonMessageComponent button:
                    writer.WriteStartObject("component");
                    writer.WriteString("label", button.Label);
                    writer.WriteNumber("style", button.Style);
                    if (button.Disable)
                    {
                        writer.WriteBoolean("disable", true);
                    }

                    if (button.Url is not null)
                    {
                        writer.WriteString("url", button.Url);
                    }

                    if (button.Icon is not null)
                    {
                        writer.WriteString("icon", button.Icon);
                    }

                    writer.WriteEndObject();
                    break;

                case SelectMessageComponent select:
                    writer.WriteStartObject("component");
                    writer.WriteNumber("type", (int)select.SelectType);
                    WriteSelectOptionsArray(writer, "options", select.Options);
                    if (select.Placeholder is not null)
                    {
                        writer.WriteString("placeholder", select.Placeholder);
                    }

                    WriteOptionalInt(writer, "min_options", select.MinOptions);
                    WriteOptionalInt(writer, "max_options", select.MaxOptions);
                    if (select.Disabled)
                    {
                        writer.WriteBoolean("disabled", true);
                    }

                    if (select.ValueSelected is MessageSelectOption selected)
                    {
                        writer.WritePropertyName("valueSelected");
                        WriteSelectOption(writer, selected);
                    }

                    writer.WriteEndObject();
                    break;

                case InputMessageComponent input:
                    writer.WriteStartObject("component");
                    if (input.NestedComponentId is not null)
                    {
                        writer.WriteString("id", input.NestedComponentId);
                    }

                    if (input.Placeholder is not null)
                    {
                        writer.WriteString("placeholder", input.Placeholder);
                    }

                    if (input.InputType is not null)
                    {
                        writer.WriteString("type", input.InputType);
                    }

                    if (input.DefaultValue is not null)
                    {
                        writer.WriteString("defaultValue", input.DefaultValue);
                    }

                    if (input.Textarea)
                    {
                        writer.WriteBoolean("textarea", true);
                    }

                    if (input.Required)
                    {
                        writer.WriteBoolean("required", true);
                    }

                    if (input.Disabled)
                    {
                        writer.WriteBoolean("disabled", true);
                    }

                    WriteOptionalInt(writer, "style", input.Style);
                    writer.WriteEndObject();
                    break;

                case DatePickerMessageComponent datePicker:
                    writer.WriteStartObject("component");
                    if (datePicker.Value is not null)
                    {
                        writer.WriteString("value", datePicker.Value);
                    }

                    writer.WriteEndObject();
                    break;

                case RadioMessageComponent radio:
                    WriteOptionalInt(writer, "max_options", radio.MaxOptions);
                    writer.WritePropertyName("component");
                    WriteRadioOptionsArray(writer, radio.Options);
                    break;

                case AnimationMessageComponent animation:
                    writer.WriteStartObject("component");
                    if (animation.UrlImage is not null)
                    {
                        writer.WriteString("url_image", animation.UrlImage);
                    }

                    if (animation.UrlPosition is not null)
                    {
                        writer.WriteString("url_position", animation.UrlPosition);
                    }

                    if (animation.PoolRows is not null)
                    {
                        writer.WriteStartArray("pool");
                        foreach (var row in animation.PoolRows)
                        {
                            writer.WriteStartArray();
                            foreach (var item in row)
                            {
                                writer.WriteStringValue(item);
                            }

                            writer.WriteEndArray();
                        }

                        writer.WriteEndArray();
                    }
                    else if (animation.Pool is not null)
                    {
                        writer.WriteStartArray("pool");
                        foreach (var item in animation.Pool)
                        {
                            writer.WriteStringValue(item);
                        }

                        writer.WriteEndArray();
                    }

                    WriteOptionalInt(writer, "repeat", animation.Repeat);
                    WriteOptionalInt(writer, "duration", animation.Duration);
                    if (animation.Vertical is bool vertical)
                    {
                        writer.WriteBoolean("vertical", vertical);
                    }

                    WriteOptionalInt(writer, "isResult", animation.IsResult);
                    writer.WriteEndObject();
                    break;

                case GridMessageComponent grid:
                    writer.WriteNumber("columns", grid.Columns);
                    writer.WriteNumber("rows", grid.Rows);
                    writer.WriteStartObject("component");
                    writer.WriteStartArray("items");
                    foreach (var item in grid.Items)
                    {
                        writer.WriteStartObject();
                        WriteOptionalInt(writer, "width", item.Width);
                        WriteOptionalInt(writer, "height", item.Height);
                        WriteOptionalInt(writer, "start_col", item.StartCol);
                        WriteOptionalInt(writer, "start_row", item.StartRow);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    if (grid.UrlImage is not null)
                    {
                        writer.WriteString("url_image", grid.UrlImage);
                    }

                    if (grid.UrlPosition is not null)
                    {
                        writer.WriteString("url_position", grid.UrlPosition);
                    }

                    writer.WriteEndObject();
                    break;

                case UnknownMessageComponent unknown:
                    writer.WritePropertyName("component");
                    unknown.ComponentPayload.WriteTo(writer);
                    break;

                default:
                    writer.WriteStartObject("component");
                    writer.WriteEndObject();
                    break;
            }

            writer.WriteEndObject();
        }

        private static void WriteSelectOptionsArray(Utf8JsonWriter writer, string propertyName, IReadOnlyList<MessageSelectOption> options)
        {
            writer.WriteStartArray(propertyName);
            foreach (var option in options)
            {
                WriteSelectOption(writer, option);
            }

            writer.WriteEndArray();
        }

        private static void WriteSelectOption(Utf8JsonWriter writer, in MessageSelectOption option)
        {
            writer.WriteStartObject();
            writer.WriteString("label", option.Label);
            writer.WriteString("value", option.Value);
            if (option.Description is not null)
            {
                writer.WriteString("description", option.Description);
            }

            if (option.Default)
            {
                writer.WriteBoolean("default", true);
            }

            writer.WriteEndObject();
        }

        private static void WriteRadioOptionsArray(Utf8JsonWriter writer, IReadOnlyList<MessageRadioOption> options)
        {
            writer.WriteStartArray();
            foreach (var option in options)
            {
                writer.WriteStartObject();
                writer.WriteString("label", option.Label);
                writer.WriteString("value", option.Value);
                if (option.Name is not null)
                {
                    writer.WriteString("name", option.Name);
                }

                if (option.Description is not null)
                {
                    writer.WriteString("description", option.Description);
                }

                WriteOptionalInt(writer, "style", option.Style);
                if (option.Disabled)
                {
                    writer.WriteBoolean("disabled", true);
                }

                WriteStringArray(writer, "extraData", option.ExtraData);

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteOptionalInt(Utf8JsonWriter writer, string propertyName, int? value)
        {
            if (value is int number)
            {
                writer.WriteNumber(propertyName, number);
            }
        }

        private static string? ReadOptionalString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return value.GetString();
        }

        private static IReadOnlyList<T>? ReadArray<T>(JsonElement root, string propertyName, Func<JsonElement, T> readItem)
        {
            if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var length = value.GetArrayLength();
            if (length == 0)
            {
                return Array.Empty<T>();
            }

            var items = new List<T>(length);
            foreach (var element in value.EnumerateArray())
            {
                items.Add(readItem(element));
            }

            return items;
        }

        private static HashtagOnMessage ReadHashtag(JsonElement element)
        {
            return new HashtagOnMessage(
                ReadStringProperty(element, "channelId"),
                ReadOptionalInt(element, "s"),
                ReadOptionalInt(element, "e"));
        }

        private static MessagePoll? ReadPoll(JsonElement root)
        {
            if (!root.TryGetProperty("question", out var question)
                && !root.TryGetProperty("answers", out _)
                && !root.TryGetProperty("answer_counts", out _))
            {
                return null;
            }

            var answers = ReadPollAnswers(root, "answers");
            return new MessagePoll(
                ReadStringProperty(root, "question"),
                ReadStringProperty(root, "question_emoji_id"),
                answers,
                ReadStringArray(root, "answer_emoji_ids"),
                ReadIntArray(root, "answer_counts"),
                ReadOptionalLong(root, "expire_time"),
                ReadOptionalBool(root, "is_closed"),
                ReadOptionalInt(root, "total_votes"),
                ReadOptionalBool(root, "allow_multiple_answers"),
                ReadIntArray(root, "user_votes"),
                ReadOptionalLong(root, "id"),
                ReadOptionalLong(root, "expire_at"),
                ReadOptionalInt(root, "type"));
        }

        private static IReadOnlyList<MessagePollAnswer>? ReadPollAnswers(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var answers = new List<MessagePollAnswer>(value.GetArrayLength());
            foreach (var element in value.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                answers.Add(new MessagePollAnswer(
                    ReadOptionalInt(element, "index") ?? 0,
                    ReadStringProperty(element, "label") ?? string.Empty));
            }

            return answers;
        }

        private static MessageCallLog? ReadCallLog(JsonElement root)
        {
            if (!root.TryGetProperty("callLog", out var value) || value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new MessageCallLog(
                ReadOptionalBool(value, "isVideo") ?? false,
                ReadOptionalInt(value, "callLogType") ?? 0,
                ReadOptionalBool(value, "showCallBack"));
        }

        private static JsonElement? ReadJsonValue(JsonElement root, string propertyName)
            => root.TryGetProperty(propertyName, out var value) ? value.Clone() : null;

        private static IReadOnlyDictionary<string, string>? ReadStringMap(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    result[property.Name] = property.Value.GetString() ?? string.Empty;
                }
            }

            return result;
        }

        private static IReadOnlyList<string>? ReadStringArray(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var result = new List<string>(value.GetArrayLength());
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    result.Add(item.GetString() ?? string.Empty);
                }
            }

            return result;
        }

        private static IReadOnlyList<int>? ReadIntArray(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var result = new List<int>(value.GetArrayLength());
            foreach (var item in value.EnumerateArray())
            {
                if (item.TryGetInt32(out var number))
                {
                    result.Add(number);
                }
            }

            return result;
        }

        private static EmojiOnMessage ReadEmoji(JsonElement element)
        {
            return new EmojiOnMessage(
                ReadStringProperty(element, "emojiid"),
                ReadOptionalInt(element, "s"),
                ReadOptionalInt(element, "e"));
        }

        private static LinkOnMessage ReadLink(JsonElement element)
        {
            return new LinkOnMessage(ReadOptionalInt(element, "s"), ReadOptionalInt(element, "e"));
        }

        private static PreOnMessage ReadPre(JsonElement element)
        {
            return new PreOnMessage(
                ReadStringProperty(element, "l"),
                ReadOptionalInt(element, "s"),
                ReadOptionalInt(element, "e"));
        }

        private static BoldOnMessage ReadBold(JsonElement element)
        {
            return new BoldOnMessage(
                ReadStringProperty(element, "l"),
                ReadOptionalInt(element, "s"),
                ReadOptionalInt(element, "e"));
        }

        private static LinkYoutubeOnMessage ReadYoutubeLink(JsonElement element)
        {
            return new LinkYoutubeOnMessage(ReadOptionalInt(element, "s"), ReadOptionalInt(element, "e"));
        }

        private static MarkdownOnMessage ReadMarkdown(JsonElement element)
        {
            Dictionary<string, JsonElement>? extensions = null;
            foreach (var property in element.EnumerateObject())
            {
                if (IsKnownMarkdownProperty(property.Name))
                {
                    continue;
                }

                extensions ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                extensions[property.Name] = property.Value.Clone();
            }

            return new MarkdownOnMessage(
                ReadStringProperty(element, "type"),
                ReadOptionalInt(element, "s"),
                ReadOptionalInt(element, "e"),
                ReadStringProperty(element, "url"),
                ReadStringProperty(element, "language"),
                extensions);
        }

        private static bool IsKnownMarkdownProperty(string propertyName) =>
            propertyName is "type" or "s" or "e" or "url" or "language";

        private static LinkVoiceRoomOnMessage ReadVoiceLink(JsonElement element)
        {
            return new LinkVoiceRoomOnMessage(ReadOptionalInt(element, "s"), ReadOptionalInt(element, "e"));
        }

        private static MessageEmbed ReadEmbed(JsonElement element)
        {
            MessageEmbedAuthor? author = null;
            if (element.TryGetProperty("author", out var authorElement) && authorElement.ValueKind == JsonValueKind.Object)
            {
                author = new MessageEmbedAuthor(
                    ReadRequiredString(authorElement, "name"),
                    ReadStringProperty(authorElement, "icon_url"),
                    ReadStringProperty(authorElement, "url"));
            }

            MessageEmbedThumbnail? thumbnail = null;
            if (element.TryGetProperty("thumbnail", out var thumbnailElement) && thumbnailElement.ValueKind == JsonValueKind.Object)
            {
                thumbnail = new MessageEmbedThumbnail(ReadRequiredString(thumbnailElement, "url"));
            }

            IReadOnlyList<MessageEmbedField>? fields = null;
            if (element.TryGetProperty("fields", out var fieldsElement) && fieldsElement.ValueKind == JsonValueKind.Array)
            {
                var parsedFields = new List<MessageEmbedField>();
                foreach (var fieldElement in fieldsElement.EnumerateArray())
                {
                    GridMessageComponent? shape = null;
                    if (fieldElement.TryGetProperty("shape", out var shapeElement)
                        && shapeElement.ValueKind == JsonValueKind.Object
                        && ReadComponent(shapeElement) is GridMessageComponent grid)
                    {
                        shape = grid;
                    }

                    IReadOnlyList<MessageComponent>? buttons = null;
                    if (fieldElement.TryGetProperty("button", out var buttonElement)
                        && buttonElement.ValueKind == JsonValueKind.Array)
                    {
                        buttons = ReadComponentList(buttonElement);
                    }

                    MessageComponent? input = null;
                    if (fieldElement.TryGetProperty("inputs", out var inputElement)
                        && inputElement.ValueKind == JsonValueKind.Object)
                    {
                        input = ReadComponent(inputElement);
                    }

                    Dictionary<string, JsonElement>? fieldExtensions = null;
                    foreach (var property in fieldElement.EnumerateObject())
                    {
                        if (property.Name is "name" or "value" or "inline" or "inputs" or "options" or "max_options" or "shape" or "button")
                        {
                            continue;
                        }

                        fieldExtensions ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                        fieldExtensions[property.Name] = property.Value.Clone();
                    }

                    parsedFields.Add(new MessageEmbedField(
                        ReadRequiredString(fieldElement, "name"),
                        ReadRequiredString(fieldElement, "value"),
                        fieldElement.TryGetProperty("inline", out var inlineElement) && inlineElement.ValueKind == JsonValueKind.True,
                        fieldElement.TryGetProperty("inputs", out var inputsElement) ? inputsElement.Clone() : null,
                        fieldElement.TryGetProperty("options", out var optionsElement) ? optionsElement.Clone() : null,
                        ReadOptionalInt(fieldElement, "max_options"),
                        shape,
                        buttons,
                        fieldExtensions,
                        input));
                }

                fields = parsedFields;
            }

            MessageEmbedImage? image = null;
            if (element.TryGetProperty("image", out var imageElement) && imageElement.ValueKind == JsonValueKind.Object)
            {
                image = new MessageEmbedImage(
                    ReadRequiredString(imageElement, "url"),
                    ReadStringProperty(imageElement, "width"),
                    ReadStringProperty(imageElement, "height"));
            }

            MessageEmbedFooter? footer = null;
            if (element.TryGetProperty("footer", out var footerElement) && footerElement.ValueKind == JsonValueKind.Object)
            {
                footer = new MessageEmbedFooter(
                    ReadRequiredString(footerElement, "text"),
                    ReadStringProperty(footerElement, "icon_url"));
            }

            Dictionary<string, JsonElement>? extensions = null;
            foreach (var property in element.EnumerateObject())
            {
                if (IsKnownEmbedProperty(property.Name))
                {
                    continue;
                }

                extensions ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                extensions[property.Name] = property.Value.Clone();
            }

            return new MessageEmbed(
                ReadStringProperty(element, "color"),
                ReadStringProperty(element, "title"),
                ReadStringProperty(element, "url"),
                author,
                ReadStringProperty(element, "description"),
                thumbnail,
                fields,
                image,
                ReadStringProperty(element, "timestamp"),
                footer,
                extensions);
        }

        private static bool IsKnownEmbedProperty(string propertyName) =>
            propertyName is "color" or "title" or "url" or "author" or "description" or "thumbnail" or "fields" or "image" or "timestamp" or "footer";

        private static IReadOnlyList<MessageActionRow>? ReadComponents(JsonElement root)
        {
            if (!root.TryGetProperty("components", out var componentsElement))
            {
                return null;
            }

            if (componentsElement.ValueKind == JsonValueKind.Array)
            {
                var rows = new List<MessageActionRow>();
                foreach (var element in componentsElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("components", out var rowComponents))
                    {
                        rows.Add(new MessageActionRow(ReadComponentList(rowComponents)));
                        continue;
                    }

                    if (element.ValueKind == JsonValueKind.Object)
                    {
                        rows.Add(new MessageActionRow(new[] { ReadComponent(element) }));
                    }
                }

                return rows;
            }

            return null;
        }

        private static IReadOnlyList<MessageComponent> ReadComponentList(JsonElement componentsElement)
        {
            var components = new List<MessageComponent>();
            if (componentsElement.ValueKind != JsonValueKind.Array)
            {
                return components;
            }

            foreach (var element in componentsElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    components.Add(ReadComponent(element));
                }
            }

            return components;
        }

        private static MessageComponent ReadComponent(JsonElement element)
        {
            var id = ReadStringProperty(element, "id") ?? string.Empty;
            var type = ReadOptionalInt(element, "type") ?? 0;
            element.TryGetProperty("component", out var componentPayload);

            return type switch
            {
                (int)MessageComponentType.Button when componentPayload.ValueKind == JsonValueKind.Object
                    => ReadButtonComponent(id, componentPayload),
                (int)MessageComponentType.Select when componentPayload.ValueKind == JsonValueKind.Object
                    => ReadSelectComponent(id, componentPayload),
                (int)MessageComponentType.Input when componentPayload.ValueKind == JsonValueKind.Object
                    => ReadInputComponent(id, componentPayload),
                (int)MessageComponentType.DatePicker when componentPayload.ValueKind is JsonValueKind.Object or JsonValueKind.Undefined
                    => ReadDatePickerComponent(id, componentPayload),
                (int)MessageComponentType.Radio when componentPayload.ValueKind == JsonValueKind.Array
                    => ReadRadioComponent(id, element, componentPayload),
                (int)MessageComponentType.Animation when componentPayload.ValueKind == JsonValueKind.Object
                    => ReadAnimationComponent(id, componentPayload),
                (int)MessageComponentType.Grid when componentPayload.ValueKind == JsonValueKind.Object
                    => ReadGridComponent(id, element, componentPayload),
                _
                    => new UnknownMessageComponent(
                        id,
                        type,
                        componentPayload.ValueKind == JsonValueKind.Undefined ? default : componentPayload.Clone()),
            };
        }

        private static ButtonMessageComponent ReadButtonComponent(string id, JsonElement componentPayload)
        {
            return new ButtonMessageComponent(
                id,
                ReadStringProperty(componentPayload, "label") ?? string.Empty,
                ReadOptionalInt(componentPayload, "style") ?? (int)MessageButtonStyle.Primary,
                componentPayload.TryGetProperty("disable", out var disable) && disable.ValueKind == JsonValueKind.True,
                ReadStringProperty(componentPayload, "url"),
                ReadStringProperty(componentPayload, "icon"));
        }

        private static SelectMessageComponent ReadSelectComponent(string id, JsonElement componentPayload)
        {
            var options = ReadSelectOptions(componentPayload, "options");
            MessageSelectOption? valueSelected = null;
            if (componentPayload.TryGetProperty("valueSelected", out var selected) && selected.ValueKind == JsonValueKind.Object)
            {
                valueSelected = ReadSelectOption(selected);
            }

            return new SelectMessageComponent(
                id,
                options,
                (MessageSelectType)(ReadOptionalInt(componentPayload, "type") ?? (int)MessageSelectType.Text),
                ReadStringProperty(componentPayload, "placeholder"),
                ReadOptionalInt(componentPayload, "min_options"),
                ReadOptionalInt(componentPayload, "max_options"),
                componentPayload.TryGetProperty("disabled", out var disabled) && disabled.ValueKind == JsonValueKind.True,
                valueSelected);
        }

        private static InputMessageComponent ReadInputComponent(string id, JsonElement componentPayload)
        {
            return new InputMessageComponent(
                id,
                ReadStringProperty(componentPayload, "placeholder"),
                ReadStringProperty(componentPayload, "type"),
                ReadStringProperty(componentPayload, "defaultValue"),
                componentPayload.TryGetProperty("textarea", out var textarea) && textarea.ValueKind == JsonValueKind.True,
                componentPayload.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.True,
                componentPayload.TryGetProperty("disabled", out var disabled) && disabled.ValueKind == JsonValueKind.True,
                ReadOptionalInt(componentPayload, "style"),
                ReadStringProperty(componentPayload, "id"));
        }

        private static DatePickerMessageComponent ReadDatePickerComponent(string id, JsonElement componentPayload)
        {
            string? value = null;
            if (componentPayload.ValueKind == JsonValueKind.Object)
            {
                value = ReadStringProperty(componentPayload, "value");
            }

            return new DatePickerMessageComponent(id, value);
        }

        private static RadioMessageComponent ReadRadioComponent(string id, JsonElement envelope, JsonElement componentPayload)
        {
            var options = new List<MessageRadioOption>();
            foreach (var element in componentPayload.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                options.Add(new MessageRadioOption(
                    ReadStringProperty(element, "label") ?? string.Empty,
                    ReadStringProperty(element, "value") ?? string.Empty,
                    ReadStringProperty(element, "name"),
                    ReadStringProperty(element, "description"),
                    ReadOptionalInt(element, "style"),
                    element.TryGetProperty("disabled", out var disabled) && disabled.ValueKind == JsonValueKind.True,
                    ReadStringArray(element, "extraData")));
            }

            return new RadioMessageComponent(id, options, ReadOptionalInt(envelope, "max_options"));
        }

        private static AnimationMessageComponent ReadAnimationComponent(string id, JsonElement componentPayload)
        {
            IReadOnlyList<string>? pool = null;
            IReadOnlyList<IReadOnlyList<string>>? poolRows = null;
            if (componentPayload.TryGetProperty("pool", out var poolElement) && poolElement.ValueKind == JsonValueKind.Array)
            {
                var firstIsArray = false;
                foreach (var item in poolElement.EnumerateArray())
                {
                    firstIsArray = item.ValueKind == JsonValueKind.Array;
                    break;
                }

                if (firstIsArray)
                {
                    var rows = new List<IReadOnlyList<string>>();
                    foreach (var rowElement in poolElement.EnumerateArray())
                    {
                        var row = new List<string>();
                        if (rowElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var cell in rowElement.EnumerateArray())
                            {
                                if (cell.ValueKind == JsonValueKind.String)
                                {
                                    row.Add(cell.GetString() ?? string.Empty);
                                }
                            }
                        }

                        rows.Add(row);
                    }

                    poolRows = rows;
                }
                else
                {
                    var flat = new List<string>();
                    foreach (var item in poolElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            flat.Add(item.GetString() ?? string.Empty);
                        }
                    }

                    pool = flat;
                }
            }

            bool? vertical = null;
            if (componentPayload.TryGetProperty("vertical", out var verticalElement))
            {
                vertical = verticalElement.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null,
                };
            }

            return new AnimationMessageComponent(
                id,
                ReadStringProperty(componentPayload, "url_image"),
                ReadStringProperty(componentPayload, "url_position"),
                pool,
                poolRows,
                ReadOptionalInt(componentPayload, "repeat"),
                ReadOptionalInt(componentPayload, "duration"),
                vertical,
                ReadOptionalInt(componentPayload, "isResult"));
        }

        private static GridMessageComponent ReadGridComponent(string id, JsonElement envelope, JsonElement componentPayload)
        {
            var items = new List<MessageGridItem>();
            if (componentPayload.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    items.Add(new MessageGridItem(
                        ReadOptionalInt(item, "width"),
                        ReadOptionalInt(item, "height"),
                        ReadOptionalInt(item, "start_col"),
                        ReadOptionalInt(item, "start_row")));
                }
            }

            return new GridMessageComponent(
                id,
                items,
                ReadOptionalInt(envelope, "columns") ?? 0,
                ReadOptionalInt(envelope, "rows") ?? 0,
                ReadStringProperty(componentPayload, "url_image"),
                ReadStringProperty(componentPayload, "url_position"));
        }

        private static IReadOnlyList<MessageSelectOption> ReadSelectOptions(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<MessageSelectOption>();
            }

            var options = new List<MessageSelectOption>();
            foreach (var element in value.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    options.Add(ReadSelectOption(element));
                }
            }

            return options;
        }

        private static MessageSelectOption ReadSelectOption(JsonElement element)
        {
            return new MessageSelectOption(
                ReadStringProperty(element, "label") ?? string.Empty,
                ReadStringProperty(element, "value") ?? string.Empty,
                ReadStringProperty(element, "description"),
                element.TryGetProperty("default", out var defaultElement) && defaultElement.ValueKind == JsonValueKind.True);
        }

        private static string? ReadStringProperty(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        }

        private static string ReadRequiredString(JsonElement element, string propertyName)
        {
            return ReadStringProperty(element, propertyName) ?? string.Empty;
        }

        private static int? ReadOptionalInt(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt32(out var number) => number,
                JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
                _ => null,
            };
        }

        private static long? ReadOptionalLong(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt64(out var number) => number,
                JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
                _ => null,
            };
        }

        private static bool? ReadOptionalBool(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
                _ => null,
            };
        }

        private static void ValidateOffsets(
            string? text,
            IReadOnlyList<HashtagOnMessage>? hashtags,
            IReadOnlyList<EmojiOnMessage>? emojis,
            IReadOnlyList<LinkOnMessage>? links,
            IReadOnlyList<MarkdownOnMessage>? markdown,
            IReadOnlyList<PreOnMessage>? pre,
            IReadOnlyList<BoldOnMessage>? bold,
            IReadOnlyList<LinkVoiceRoomOnMessage>? voiceLinks,
            IReadOnlyList<LinkYoutubeOnMessage>? youtubeLinks)
        {
            if (text is null)
            {
                return;
            }

            var length = text.Length;
            ValidateOffsetRange(hashtags, length, static item => (item.Start, item.End));
            ValidateOffsetRange(emojis, length, static item => (item.Start, item.End));
            ValidateOffsetRange(links, length, static item => (item.Start, item.End));
            ValidateOffsetRange(markdown, length, static item => (item.Start, item.End));
            ValidateOffsetRange(pre, length, static item => (item.Start, item.End));
            ValidateOffsetRange(bold, length, static item => (item.Start, item.End));
            ValidateOffsetRange(voiceLinks, length, static item => (item.Start, item.End));
            ValidateOffsetRange(youtubeLinks, length, static item => (item.Start, item.End));
        }

        private static void ValidateOffsetRange<T>(IReadOnlyList<T>? items, int textLength, Func<T, (int? Start, int? End)> selector)
        {
            if (items is null)
            {
                return;
            }

            foreach (var item in items)
            {
                var (start, end) = selector(item);
                if (start is int startIndex && (startIndex < 0 || startIndex > textLength))
                {
                    throw new ArgumentOutOfRangeException(nameof(start), startIndex, $"Start offset must be between 0 and {textLength} UTF-16 code units.");
                }

                if (end is int endIndex && (endIndex < 0 || endIndex > textLength))
                {
                    throw new ArgumentOutOfRangeException(nameof(end), endIndex, $"End offset must be between 0 and {textLength} UTF-16 code units.");
                }
            }
        }
    }

}
