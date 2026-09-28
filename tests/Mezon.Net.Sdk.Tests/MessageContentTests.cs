using System;
using System.Collections.Generic;
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
                    new MessageRadioOption("Yes", "1", ExtraData: new[] { "meta-1" }),
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
            Assert.Contains("\"extraData\":[\"meta-1\"]", json);
        }

        [Fact]
        public void ButtonBuilder_writes_animation_and_grid_shapes()
        {
            var json = new ButtonBuilder()
                .AddAnimation("anim-1", "image.png", "center", new[] { "a.png", "b.png" }, repeat: 2, duration: 3)
                .AddGrid(
                    "grid-1",
                    new[] { new MessageGridItem(Width: 1, Height: 2, StartCol: 0, StartRow: 1) },
                    columns: 2,
                    rows: 1,
                    urlImage: "grid.png")
                .Build();

            Assert.Contains("\"type\":6", json);
            Assert.Contains("\"type\":7", json);
            Assert.Contains("\"url_image\":\"image.png\"", json);
            Assert.Contains("\"columns\":2", json);
            Assert.Contains("\"start_row\":1", json);
        }

        [Fact]
        public void ButtonBuilder_writes_full_animation_and_input_shapes()
        {
            var json = new ButtonBuilder()
                .AddInput(
                    "input-1",
                    placeholder: "name",
                    required: true,
                    style: 2,
                    nestedComponentId: "input-component")
                .AddAnimation(
                    "animation-1",
                    urlImage: "image.png",
                    urlPosition: "center",
                    poolRows: new IReadOnlyList<string>[] { new[] { "a", "b" } },
                    vertical: true,
                    isResult: 1)
                .Build();

            Assert.Contains("\"id\":\"input-component\"", json);
            Assert.Contains("\"required\":true", json);
            Assert.Contains("\"style\":2", json);
            Assert.Contains("\"pool\":[[\"a\",\"b\"]]", json);
            Assert.Contains("\"vertical\":true", json);
            Assert.Contains("\"isResult\":1", json);

            var content = MessageContent.Parse($"{{\"components\":[{{\"components\":{json}}}]}}");
            var row = Assert.Single(content.Components!);
            var input = Assert.IsType<InputMessageComponent>(row.Components[0]);
            Assert.Equal("input-component", input.NestedComponentId);
            var animation = Assert.IsType<AnimationMessageComponent>(row.Components[1]);
            Assert.True(animation.Vertical);
            Assert.Equal(1, animation.IsResult);
            Assert.Equal(new[] { "a", "b" }, Assert.Single(animation.PoolRows!));
        }

        [Fact]
        public void MessageEmbedBuilder_accepts_typed_embed_parts()
        {
            var content = new MessageContentBuilder()
                .AddEmbed(new MessageEmbedBuilder()
                    .SetAuthorValue(new MessageEmbedAuthor("Monze", "icon.png", "https://example.test"))
                    .SetThumbnailValue(new MessageEmbedThumbnail("thumb.png"))
                    .AddFieldValue(new MessageEmbedField("Status", "Ready", inline: true))
                    .SetImageValue(new MessageEmbedImage("image.png", "640", "480"))
                    .SetFooterValue(new MessageEmbedFooter("Footer", "footer.png"))
                    .Build())
                .Build();

            var embed = Assert.Single(MessageContent.Parse(content.ToJson()).Embeds!);
            Assert.Equal("Monze", embed.Author!.Name);
            Assert.Equal("thumb.png", embed.Thumbnail!.Url);
            Assert.True(Assert.Single(embed.Fields!).Inline);
            Assert.Equal("640", embed.Image!.Width);
            Assert.Equal("Footer", embed.Footer!.Text);
        }

        [Fact]
        public void ButtonBuilder_is_immutable_after_build()
        {
            var builder = new ButtonBuilder().AddButton("btn-1", "Go");
            builder.Build();
            Assert.Throws<InvalidOperationException>(() => builder.AddButton("btn-2", "Again"));
        }

        [Fact]
        public void ButtonBuilder_accepts_unknown_components_for_forward_compatibility()
        {
            using var payload = JsonDocument.Parse("{\"future\":true}");

            var json = new ButtonBuilder()
                .AddComponent(new UnknownMessageComponent("future-1", 99, payload.RootElement))
                .Build();

            Assert.Equal("[{\"id\":\"future-1\",\"type\":99,\"component\":{\"future\":true}}]", json);
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

        [Fact]
        public void MessageContentBuilder_rejects_known_wire_properties_as_extensions()
        {
            using var value = JsonDocument.Parse("true");

            Assert.Throws<ArgumentException>(() => new MessageContentBuilder().SetExtension("t", value.RootElement));
            Assert.Throws<ArgumentException>(() => new MessageContentBuilder().AddMarkdown(
                "link",
                extensions: new Dictionary<string, JsonElement> { ["type"] = value.RootElement }));
        }

        [Fact]
        public void MessageEmbedBuilder_rejects_known_wire_properties_as_extensions()
        {
            using var value = JsonDocument.Parse("true");
            var fieldExtensions = new Dictionary<string, JsonElement> { ["name"] = value.RootElement };

            Assert.Throws<ArgumentException>(() => new MessageEmbedBuilder().SetExtension("title", value.RootElement));
            Assert.Throws<ArgumentException>(() => new MessageEmbedBuilder().AddField(
                "Name",
                "Value",
                extensions: fieldExtensions));
        }

        [Fact]
        public void MessageContentCodec_rejects_duplicate_properties_from_direct_models()
        {
            using var value = JsonDocument.Parse("true");
            var embed = new MessageEmbed(
                title: "Typed title",
                extensions: new Dictionary<string, JsonElement> { ["title"] = value.RootElement });

            Assert.Throws<InvalidOperationException>(() => new MessageContentBuilder().AddEmbed(embed).Build());
        }

        [Fact]
        public void MessageContentBuilder_round_trips_legacy_roots_and_embed_controls()
        {
            var grid = new GridMessageComponent(
                "grid-1",
                new[] { new MessageGridItem(Width: 1, Height: 1) },
                columns: 1,
                rows: 1);
            var buttons = new ButtonBuilder()
                .AddButton("field-button", "Apply", style: (int)MessageButtonStyle.Success)
                .BuildComponents();

            var content = new MessageContentBuilder()
                .SetText("legacy and controls")
                .AddPre("csharp", 0, 6)
                .AddBold(null, 7, 13)
                .AddYoutubeLink(14, 19)
                .AddEmbed(new MessageEmbedBuilder()
                    .AddField("Controls", "Choose", shape: grid, buttons: buttons)
                    .Build())
                .Build();

            var parsed = MessageContent.Parse(content.ToJson());
            Assert.Equal("csharp", Assert.Single(parsed.Pre!).Language);
            Assert.Equal(7, Assert.Single(parsed.Bold!).Start);
            Assert.Equal(14, Assert.Single(parsed.YoutubeLinks!).Start);

            var field = Assert.Single(Assert.Single(parsed.Embeds!).Fields!);
            Assert.NotNull(field.Shape);
            Assert.Equal(1, field.Shape!.Columns);
            var button = Assert.IsType<ButtonMessageComponent>(Assert.Single(field.Buttons!));
            Assert.Equal("Apply", button.Label);
            Assert.Contains("\"shape\"", content.ToJson());
            Assert.Contains("\"button\"", content.ToJson());
        }

        [Fact]
        public void MessageEmbedBuilder_round_trips_typed_embed_input_and_radio_extra_data()
        {
            var radio = new RadioMessageComponent(
                "search-radio",
                new[]
                {
                    new MessageRadioOption(
                        "Result",
                        "result-1",
                        Name: "Result",
                        ExtraData: new[] { "source-token" })
                },
                maxOptions: 1);

            var content = new MessageContentBuilder()
                .AddEmbed(new MessageEmbedBuilder()
                    .AddInputField("Results", "Choose one", radio)
                    .Build())
                .Build();

            var json = content.ToJson();
            Assert.Contains("\"inputs\"", json);
            Assert.Contains("\"extraData\":[\"source-token\"]", json);

            var field = Assert.Single(Assert.Single(MessageContent.Parse(json).Embeds!).Fields!);
            var parsedRadio = Assert.IsType<RadioMessageComponent>(field.Input);
            Assert.Equal("search-radio", parsedRadio.Id);
            Assert.Equal("source-token", Assert.Single(Assert.Single(parsedRadio.Options).ExtraData!));
        }

        [Fact]
        public void MessageContentBuilder_round_trips_poll_call_log_and_message_metadata()
        {
            using var canvas = JsonDocument.Parse("{\"id\":\"canvas-1\"}");
            var content = new MessageContentBuilder()
                .SetText("poll")
                .SetE2ee(1)
                .SetCanvas(canvas.RootElement)
                .SetCanvasTitles(new Dictionary<string, string> { ["canvas-1"] = "Demo" })
                .SetCallLog(new MessageCallLogBuilder()
                    .SetVideo(true)
                    .SetCallLogType(1)
                    .SetShowCallBack(false))
                .SetType("card")
                .SetChannelId("channel-label")
                .SetForwarded(true)
                .SetIsCard(true)
                .SetReplyToMessageId(7)
                .SetLastSeenSeconds(8)
                .SetPoll(new MessagePollBuilder()
                    .SetQuestion("Best color?")
                    .AddAnswer(0, "blue")
                    .SetAnswerCounts(new[] { 2 })
                    .SetTotalVotes(2)
                    .SetAllowMultipleAnswers(false)
                    .SetType(0))
                .SetPresignFinish(new[] { "upload-1" })
                .SetCreateTimeSeconds(9)
                .Build();

            var parsed = MessageContent.Parse(content.ToJson());
            Assert.Equal(1, parsed.E2ee);
            Assert.Equal("canvas-1", parsed.Canvas!.Value.GetProperty("id").GetString());
            Assert.Equal("Demo", parsed.CanvasTitles!["canvas-1"]);
            Assert.True(parsed.CallLog!.Value.IsVideo);
            Assert.False(parsed.CallLog.Value.ShowCallBack);
            Assert.True(parsed.Forwarded);
            Assert.True(parsed.IsCard);
            Assert.Equal(7, parsed.ReplyToMessageId);
            Assert.Equal("Best color?", parsed.Poll!.Question);
            Assert.Equal("blue", Assert.Single(parsed.Poll.Answers!).Label);
            Assert.Equal("upload-1", Assert.Single(parsed.PresignFinish!));
            Assert.Equal(9, parsed.CreateTimeSeconds);
        }

        [Fact]
        public void Poll_and_call_log_builders_are_immutable_after_build()
        {
            var poll = new MessagePollBuilder().SetQuestion("Question");
            poll.Build();
            Assert.Throws<InvalidOperationException>(() => poll.SetType(1));

            var callLog = new MessageCallLogBuilder().SetCallLogType(1);
            callLog.Build();
            Assert.Throws<InvalidOperationException>(() => callLog.SetVideo(true));
        }
    }
}
