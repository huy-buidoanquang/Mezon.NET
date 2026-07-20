using System;
using System.Collections.Generic;
using Mezon.Net.Client;

namespace Mezon.Net.Sdk.Builders
{
    /// <summary>
    /// Builds a flat mezon <c>components</c> array (same shape as JS <c>ButtonBuilder</c>),
    /// supporting the full <see cref="MessageComponentType"/> set.
    /// </summary>
    public sealed class ButtonBuilder
    {
        private readonly List<MessageComponent> _components = new();
        private IReadOnlyList<MessageComponent>? _snapshot;

        public ButtonBuilder AddButton(
            string id,
            string label,
            int style = (int)MessageButtonStyle.Primary,
            bool disable = false,
            string? url = null,
            string? icon = null)
        {
            EnsureMutable();
            _components.Add(new ButtonMessageComponent(id, label, style, disable, url, icon));
            return this;
        }

        public ButtonBuilder AddSelect(
            string id,
            IReadOnlyList<MessageSelectOption> options,
            MessageSelectType selectType = MessageSelectType.Text,
            string? placeholder = null,
            int? minOptions = null,
            int? maxOptions = null,
            bool disabled = false,
            MessageSelectOption? valueSelected = null)
        {
            EnsureMutable();
            _components.Add(new SelectMessageComponent(
                id,
                options,
                selectType,
                placeholder,
                minOptions,
                maxOptions,
                disabled,
                valueSelected));
            return this;
        }

        public ButtonBuilder AddInput(
            string id,
            string? placeholder = null,
            string? inputType = "text",
            string? defaultValue = null,
            bool textarea = false,
            bool required = false,
            bool disabled = false,
            int? style = null)
        {
            EnsureMutable();
            _components.Add(new InputMessageComponent(
                id,
                placeholder,
                inputType,
                defaultValue,
                textarea,
                required,
                disabled,
                style,
                nestedComponentId: $"{id}-component"));
            return this;
        }

        public ButtonBuilder AddDatePicker(string id, string? value = null)
        {
            EnsureMutable();
            _components.Add(new DatePickerMessageComponent(id, value));
            return this;
        }

        public ButtonBuilder AddRadio(string id, IReadOnlyList<MessageRadioOption> options, int? maxOptions = null)
        {
            EnsureMutable();
            _components.Add(new RadioMessageComponent(id, options, maxOptions));
            return this;
        }

        public ButtonBuilder AddAnimation(
            string id,
            string urlImage,
            string urlPosition,
            IReadOnlyList<string> pool,
            int? repeat = null,
            int? duration = null)
        {
            EnsureMutable();
            _components.Add(new AnimationMessageComponent(
                id,
                urlImage,
                urlPosition,
                pool,
                poolRows: null,
                repeat,
                duration));
            return this;
        }

        public ButtonBuilder AddGrid(
            string id,
            IReadOnlyList<MessageGridItem> items,
            int columns,
            int rows,
            string? urlImage = null,
            string? urlPosition = null)
        {
            EnsureMutable();
            _components.Add(new GridMessageComponent(id, items, columns, rows, urlImage, urlPosition));
            return this;
        }

        public string Build()
        {
            EnsureMutable();
            _snapshot = _components.ToArray();
            var json = MessageContentCodec.SerializeComponentList(_snapshot);
            _components.Clear();
            return json;
        }

        public IReadOnlyList<MessageComponent> BuildComponents()
        {
            EnsureMutable();
            _snapshot = _components.ToArray();
            _components.Clear();
            return _snapshot;
        }

        private void EnsureMutable()
        {
            if (_snapshot is not null)
            {
                throw new InvalidOperationException("Builder already built.");
            }
        }
    }
}
