using System;
using System.IO;
using System.Text;
using System.Text.Json;
namespace Mezon.Net.Sdk.Builders
{
    public sealed class InteractiveBuilder
    {
        private readonly Utf8JsonWriter _writer;
        private readonly MemoryStream _stream;
        private bool _built;

        public InteractiveBuilder(string? title = null)
        {
            _stream = new MemoryStream();
            _writer = new Utf8JsonWriter(_stream);
            _writer.WriteStartObject();
            if (!string.IsNullOrEmpty(title))
            {
                _writer.WriteString("title", title);
            }

            _writer.WriteStartArray("fields");
        }

        public InteractiveBuilder SetDescription(string description)
        {
            _writer.WriteString("description", description);
            return this;
        }

        public InteractiveBuilder AddField(string name, string value, bool inline = false)
        {
            _writer.WriteStartObject();
            _writer.WriteString("name", name);
            _writer.WriteString("value", value);
            _writer.WriteBoolean("inline", inline);
            _writer.WriteEndObject();
            return this;
        }

        public string Build()
        {
            if (_built)
            {
                throw new InvalidOperationException("Builder already built.");
            }

            _writer.WriteEndArray();
            _writer.WriteEndObject();
            _writer.Flush();
            _built = true;
            return Encoding.UTF8.GetString(_stream.ToArray());
        }
    }
}
