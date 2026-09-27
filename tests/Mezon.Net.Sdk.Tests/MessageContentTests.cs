using System;
using System.Text.Json;
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

        [Fact]
        public void MessageContentBuilder_round_trips_all_typed_roots()
        {
            using var extension = JsonDocument.Parse("{\"source\":\"test\"}");
            var content = new MessageContentBuilder()
                .SetText("hello world with voice room")
                .AddHashtag("channel-label", 0, 5)
                .AddEmoji("emoji-1", 5, 6)
                .AddLink(6, 12)
                .AddMarkdown(MarkdownMarkerType.Bold, 0, 5)
                .AddVoiceLink(12, 18)
                .AddEmbed(new MessageEmbedBuilder()
                    .SetTitle("Title")
                    .AddField("Status", "Ready")
                    .Build())
                .AddActionRow(new ButtonBuilder().AddButton("ok", "OK").BuildComponents())
                .SetExtension("custom", extension.RootElement)
                .Build();

            var parsed = MessageContent.Parse(content.ToJson());
            Assert.Equal("hello world with voice room", parsed.Text);
            Assert.Single(parsed.Hashtags!);
            Assert.Single(parsed.Emojis!);
            Assert.Single(parsed.Links!);
            Assert.Single(parsed.Markdown!);
            Assert.Single(parsed.VoiceLinks!);
            Assert.Equal("Title", Assert.Single(parsed.Embeds!).Title);
            Assert.Single(parsed.Components!);
            Assert.Equal("Ready", Assert.Single(parsed.Embeds!).Fields![0].Value);
            Assert.Equal("test", parsed.UnknownExtensions!["custom"].GetProperty("source").GetString());
        }

        [Fact]
        public void MessageContentBuilder_is_immutable_after_build()
        {
            var builder = new MessageContentBuilder().SetText("hello");
            builder.Build();
            Assert.Throws<InvalidOperationException>(() => builder.SetText("again"));
        }
    }
}
