using System;
using System.Collections.Generic;
using System.Text.Json;
using Mezon.Net.Client;

namespace Mezon.Net.Sdk.Builders;

/// <summary>Builds one typed Mezon embed without hand-written wire JSON.</summary>
public sealed class MessageEmbedBuilder
{
    private readonly List<MessageEmbedField> _fields = new();
    private readonly Dictionary<string, JsonElement> _extensions = new(StringComparer.Ordinal);
    private bool _built;
    private string? _color;
    private string? _title;
    private string? _url;
    private MessageEmbedAuthor? _author;
    private string? _description;
    private MessageEmbedThumbnail? _thumbnail;
    private MessageEmbedImage? _image;
    private string? _timestamp;
    private MessageEmbedFooter? _footer;

    public MessageEmbedBuilder SetColor(string? color)
    {
        EnsureMutable();
        _color = color;
        return this;
    }

    public MessageEmbedBuilder SetTitle(string? title)
    {
        EnsureMutable();
        _title = title;
        return this;
    }

    public MessageEmbedBuilder SetUrl(string? url)
    {
        EnsureMutable();
        _url = url;
        return this;
    }

    public MessageEmbedBuilder SetAuthor(string name, string? iconUrl = null, string? url = null)
    {
        EnsureMutable();
        _author = new MessageEmbedAuthor(name, iconUrl, url);
        return this;
    }

    public MessageEmbedBuilder SetAuthorValue(MessageEmbedAuthor author)
    {
        EnsureMutable();
        _author = author ?? throw new ArgumentNullException(nameof(author));
        return this;
    }

    public MessageEmbedBuilder SetDescription(string? description)
    {
        EnsureMutable();
        _description = description;
        return this;
    }

    public MessageEmbedBuilder SetThumbnail(string? url)
    {
        EnsureMutable();
        _thumbnail = url is null ? null : new MessageEmbedThumbnail(url);
        return this;
    }

    public MessageEmbedBuilder SetThumbnailValue(MessageEmbedThumbnail thumbnail)
    {
        EnsureMutable();
        _thumbnail = thumbnail ?? throw new ArgumentNullException(nameof(thumbnail));
        return this;
    }

    public MessageEmbedBuilder AddField(
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
        EnsureMutable();
        ValidateFieldExtensionNames(extensions);
        _fields.Add(new MessageEmbedField(name, value, inline, inputs, options, maxOptions, shape, buttons, extensions, input));
        return this;
    }

    /// <summary>Adds a field whose interactive control is serialized as <c>inputs</c>.</summary>
    public MessageEmbedBuilder AddInputField(
        string name,
        string value,
        MessageComponent input,
        bool inline = false,
        JsonElement? options = null,
        int? maxOptions = null)
    {
        EnsureMutable();
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        _fields.Add(new MessageEmbedField(
            name,
            value,
            inline,
            inputs: null,
            options,
            maxOptions,
            shape: null,
            buttons: null,
            extensions: null,
            input: input));
        return this;
    }

    public MessageEmbedBuilder AddFieldValue(MessageEmbedField field)
    {
        EnsureMutable();
        if (field is null)
        {
            throw new ArgumentNullException(nameof(field));
        }

        ValidateFieldExtensionNames(field.Extensions);
        _fields.Add(field);
        return this;
    }

    public MessageEmbedBuilder SetImage(string? url, string? width = null, string? height = null)
    {
        EnsureMutable();
        _image = url is null ? null : new MessageEmbedImage(url, width, height);
        return this;
    }

    public MessageEmbedBuilder SetImageValue(MessageEmbedImage image)
    {
        EnsureMutable();
        _image = image ?? throw new ArgumentNullException(nameof(image));
        return this;
    }

    public MessageEmbedBuilder SetTimestamp(string? timestamp)
    {
        EnsureMutable();
        _timestamp = timestamp;
        return this;
    }

    public MessageEmbedBuilder SetFooter(string? text, string? iconUrl = null)
    {
        EnsureMutable();
        _footer = text is null ? null : new MessageEmbedFooter(text, iconUrl);
        return this;
    }

    public MessageEmbedBuilder SetFooterValue(MessageEmbedFooter footer)
    {
        EnsureMutable();
        _footer = footer ?? throw new ArgumentNullException(nameof(footer));
        return this;
    }

    public MessageEmbedBuilder SetExtension(string name, JsonElement value)
    {
        EnsureMutable();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Extension name is required.", nameof(name));
        }

        if (name is "color" or "title" or "url" or "author" or "description" or "thumbnail" or "fields" or "image" or "timestamp" or "footer")
        {
            throw new ArgumentException($"'{name}' is a known embed property; use its typed builder method.", nameof(name));
        }

        _extensions[name] = value.Clone();
        return this;
    }

    public MessageEmbed Build()
    {
        EnsureMutable();
        _built = true;
        return new MessageEmbed(
            _color,
            _title,
            _url,
            _author,
            _description,
            _thumbnail,
            _fields.Count == 0 ? null : _fields.ToArray(),
            _image,
            _timestamp,
            _footer,
            _extensions.Count == 0 ? null : new Dictionary<string, JsonElement>(_extensions, StringComparer.Ordinal));
    }

    private void EnsureMutable()
    {
        if (_built)
        {
            throw new InvalidOperationException("Builder already built.");
        }
    }

    private static void ValidateFieldExtensionNames(IReadOnlyDictionary<string, JsonElement>? extensions)
    {
        if (extensions is null)
        {
            return;
        }

        foreach (var pair in extensions)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Extension names are required.", nameof(extensions));
            }

            if (pair.Key is "name" or "value" or "inline" or "inputs" or "options" or "max_options" or "shape" or "button")
            {
                throw new ArgumentException($"'{pair.Key}' is a known embed field property; use its typed builder method.", nameof(extensions));
            }
        }
    }
}
