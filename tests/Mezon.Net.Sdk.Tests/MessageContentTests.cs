using System;
using Mezon.Net.Client;
using Mezon.Net.Sdk.Builders;
using Xunit;

namespace Mezon.Net.Sdk.Tests
{
    public class MessageContentTests
    {
        [Fact]
        public void ButtonBuilder_writes_component_shape()
        {
            var json = new ButtonBuilder()
                .AddButton("btn-1", "Click me", 2)
                .Build();

            Assert.Equal("[{\"id\":\"btn-1\",\"type\":1,\"component\":{\"label\":\"Click me\",\"style\":2}}]", json);
        }

        [Fact]
        public void ButtonBuilder_writes_select_input_radio_shapes()
        {
            var json = new ButtonBuilder()
                .AddSelect("sel-1", new[]
                {
                    new MessageSelectOption("A", "a"),
                    new MessageSelectOption("B", "b"),
                })
                .AddInput("inp-1", placeholder: "name")
                .AddRadio("rad-1", new[]
                {
                    new MessageRadioOption("Yes", "1"),
                    new MessageRadioOption("No", "0"),
                }, maxOptions: 1)
                .AddDatePicker("date-1")
                .Build();

            Assert.Contains("\"type\":2", json);
            Assert.Contains("\"type\":3", json);
            Assert.Contains("\"type\":5", json);
            Assert.Contains("\"type\":4", json);
            Assert.Contains("\"options\"", json);
            Assert.Contains("\"placeholder\":\"name\"", json);
            Assert.Contains("\"max_options\":1", json);
        }

        [Fact]
        public void ButtonBuilder_is_immutable_after_build()
        {
            var builder = new ButtonBuilder().AddButton("btn-1", "Go");
            builder.Build();
            Assert.Throws<InvalidOperationException>(() => builder.AddButton("btn-2", "Again"));
        }
    }
}
