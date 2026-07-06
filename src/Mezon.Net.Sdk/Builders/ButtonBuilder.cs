using System.Text;
using System.Text.Json;

namespace Mezon.Net.Sdk.Builders
{
    public sealed class ButtonBuilder
    {
        private readonly StringBuilder _buffer = new StringBuilder();
        private bool _started;

        public ButtonBuilder AddButton(string id, string label, int style = 1)
        {
            if (!_started)
            {
                _buffer.Append('[');
                _started = true;
            }
            else
            {
                _buffer.Append(',');
            }

            _buffer.Append("{\"id\":\"").Append(id).Append("\",\"label\":\"").Append(label).Append("\",\"style\":").Append(style).Append('}');
            return this;
        }

        public string Build()
        {
            if (!_started)
            {
                return "[]";
            }

            _buffer.Append(']');
            return _buffer.ToString();
        }
    }
}
