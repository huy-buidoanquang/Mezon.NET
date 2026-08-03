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
        private bool _fieldsStarted;

        public InteractiveBuilder(string? title = null)
        {
            _stream = new MemoryStream();
            _writer = new Utf8JsonWriter(_stream);
            _writer.WriteStartObject();
            if (!string.IsNullOrEmpty(title))
            {
                _writer.WriteString("title", title);
            }
        }

        public InteractiveBuilder SetDescription(string description)
        {
            EnsureNotBuilt();
            _writer.WriteString("description", description);
            return this;
        }

        public InteractiveBuilder AddField(string name, string value, bool inline = false)
        {
            EnsureNotBuilt();
            EnsureFieldsArray();
            _writer.WriteStartObject();
            _writer.WriteString("name", name);
            _writer.WriteString("value", value);
            if (inline)
            {
                _writer.WriteBoolean("inline", inline);
            }

            _writer.WriteEndObject();
            return this;
        }

        public string Build()
        {
            if (_built)
            {
                throw new InvalidOperationException("Builder already built.");
            }

            if (_fieldsStarted)
            {
                _writer.WriteEndArray();
            }

            _writer.WriteEndObject();
            _writer.Flush();
            _built = true;
            return Encoding.UTF8.GetString(_stream.ToArray());
        }

        private void EnsureFieldsArray()
        {
            if (_fieldsStarted)
            {
                return;
            }

            _writer.WriteStartArray("fields");
            _fieldsStarted = true;
        }

        private void EnsureNotBuilt()
        {
            if (_built)
            {
                throw new InvalidOperationException("Builder already built.");
            }
        }
    }
}
