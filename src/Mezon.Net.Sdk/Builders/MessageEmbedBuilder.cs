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

    public MessageEmbedBuilder AddField(
        string name,
        string value,
        bool inline = false,
        JsonElement? inputs = null,
        JsonElement? options = null,
        int? maxOptions = null)
    {
        EnsureMutable();
        _fields.Add(new MessageEmbedField(name, value, inline, inputs, options, maxOptions));
        return this;
    }

    public MessageEmbedBuilder SetImage(string? url, string? width = null, string? height = null)
    {
        EnsureMutable();
        _image = url is null ? null : new MessageEmbedImage(url, width, height);
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

    public MessageEmbedBuilder SetExtension(string name, JsonElement value)
    {
        EnsureMutable();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Extension name is required.", nameof(name));
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
}
