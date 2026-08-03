using System;
using Mezon.Net.Client;

namespace Mezon.Net.Client.Tests
{
    public class MessageContentTests
    {
        [Fact]
        public void Text_roundtrips_to_json_payload()
        {
            var content = MessageContent.CreateText("hi");
            Assert.Equal("hi", content.Text);
            Assert.Equal("{\"t\":\"hi\"}", content.ToJson());
        }

        [Fact]
        public void Parse_preserves_exact_raw_json()
        {
            const string raw = "{\"t\":\"hi\",\"custom\":true}";
            var content = MessageContent.Parse(raw);
            Assert.Equal(raw, content.RawJson);
            Assert.Equal("hi", content.Text);
            Assert.Equal(raw, content.ToJson());
        }

        [Fact]
        public void Parse_preserves_poll_and_canvas_via_raw_passthrough()
        {
            const string raw =
                "{\"t\":\"vote?\",\"question\":\"Best color?\",\"answers\":[\"red\",\"blue\"],\"canvas\":{\"id\":\"c1\"},\"callLog\":{\"duration\":12}}";
            var content = MessageContent.Parse(raw);
            Assert.Equal(raw, content.ToJson());
            Assert.Equal("vote?", content.Text);
            Assert.NotNull(content.UnknownExtensions);
            Assert.True(content.UnknownExtensions!.ContainsKey("question"));
            Assert.True(content.UnknownExtensions.ContainsKey("answers"));
            Assert.True(content.UnknownExtensions.ContainsKey("canvas"));
            Assert.True(content.UnknownExtensions.ContainsKey("callLog"));
        }

        [Fact]
        public void Parse_reads_mk_subtypes_including_lk_yt()
        {
            const string raw =
                "{\"t\":\"ab**cd**efyt\",\"mk\":[{\"type\":\"b\",\"s\":2,\"e\":6},{\"type\":\"pre\",\"s\":0,\"e\":2,\"language\":\"cs\"},{\"type\":\"lk_yt\",\"s\":8,\"e\":10,\"url\":\"https://youtu.be/x\"}]}";
            var content = MessageContent.Parse(raw);
            Assert.Equal("ab**cd**efyt", content.Text);
            Assert.NotNull(content.Markdown);
            Assert.Equal(3, content.Markdown!.Count);
            Assert.Equal(MarkdownMarkerType.Bold, content.Markdown[0].Type);
            Assert.Equal(2, content.Markdown[0].Start);
            Assert.Equal(MarkdownMarkerType.Pre, content.Markdown[1].Type);
            Assert.Equal("cs", content.Markdown[1].Language);
            Assert.Equal(MarkdownMarkerType.LinkYoutube, content.Markdown[2].Type);
            Assert.Equal("https://youtu.be/x", content.Markdown[2].Url);
            Assert.Equal(raw, content.ToJson());
        }

        [Fact]
        public void Parse_keeps_legacy_root_pre_bm_lky_in_unknown()
        {
            const string raw =
                "{\"t\":\"hi\",\"pre\":[{\"l\":\"cs\",\"s\":0,\"e\":2}],\"bm\":[{\"s\":0,\"e\":2}],\"lky\":[{\"s\":0,\"e\":2}]}";
            var content = MessageContent.Parse(raw);
            Assert.Equal(raw, content.ToJson());
            Assert.NotNull(content.UnknownExtensions);
            Assert.True(content.UnknownExtensions!.ContainsKey("pre"));
            Assert.True(content.UnknownExtensions.ContainsKey("bm"));
            Assert.True(content.UnknownExtensions.ContainsKey("lky"));
            Assert.Null(content.Markdown);
        }

        [Fact]
        public void Text_fast_path_does_not_require_snapshot_materialization()
        {
            const string raw = "{\"t\":\"hello\",\"embed\":[{\"title\":\"x\"}],\"custom\":1}";
            var content = MessageContent.Parse(raw);
            Assert.Equal("hello", content.Text);
            Assert.Equal(raw, content.ToJson());
        }

        [Fact]
        public void Parse_validates_unicode_offsets_using_utf16_code_units()
        {
            const string raw = "{\"t\":\"hi👋\",\"ej\":[{\"emojiid\":\"1\",\"s\":2,\"e\":4}]}";
            var content = MessageContent.Parse(raw);
            Assert.Equal("hi👋", content.Text);
            Assert.NotNull(content.Emojis);
            Assert.Equal(2, content.Emojis![0].Start);
            Assert.Equal(4, content.Emojis[0].End);
        }

        [Fact]
        public void Parse_rejects_offsets_outside_utf16_text_length_on_typed_access()
        {
            const string raw = "{\"t\":\"abc\",\"lk\":[{\"s\":0,\"e\":9}]}";
            var content = MessageContent.Parse(raw);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = content.Links);
        }

        [Fact]
        public void Parse_falls_back_to_text_payload_for_malformed_json()
        {
            const string malformed = "not-json";
            var content = MessageContent.Parse(malformed);
            Assert.Equal("not-json", content.Text);
            Assert.Equal("{\"t\":\"not-json\"}", content.ToJson());
        }

        [Fact]
        public void TryParse_returns_false_for_null()
        {
            Assert.False(MessageContent.TryParse(null, out _));
        }

        [Fact]
        public void TryReadTextProperty_reads_root_t()
        {
            Assert.Equal("hi", MessageContentCodec.TryReadTextProperty("{\"t\":\"hi\",\"x\":1}"));
            Assert.Null(MessageContentCodec.TryReadTextProperty("{\"x\":1}"));
            Assert.Null(MessageContentCodec.TryReadTextProperty("[]"));
        }

        [Fact]
        public void Parse_reads_all_message_component_types()
        {
            const string raw =
                "{\"t\":\"x\",\"components\":[{\"components\":[" +
                "{\"id\":\"b1\",\"type\":1,\"component\":{\"label\":\"Go\",\"style\":2,\"disable\":true}}," +
                "{\"id\":\"s1\",\"type\":2,\"component\":{\"type\":1,\"options\":[{\"label\":\"A\",\"value\":\"a\"}],\"placeholder\":\"pick\"}}," +
                "{\"id\":\"i1\",\"type\":3,\"component\":{\"id\":\"i1-component\",\"placeholder\":\"n\",\"type\":\"text\",\"defaultValue\":\"\",\"textarea\":false}}," +
                "{\"id\":\"d1\",\"type\":4,\"component\":{}}," +
                "{\"id\":\"r1\",\"type\":5,\"max_options\":2,\"component\":[{\"label\":\"Y\",\"value\":\"1\"}]}," +
                "{\"id\":\"a1\",\"type\":6,\"component\":{\"url_image\":\"u\",\"url_position\":\"p\",\"pool\":[\"a\",\"b\"],\"repeat\":1}}," +
                "{\"id\":\"g1\",\"type\":7,\"columns\":2,\"rows\":2,\"component\":{\"items\":[{\"width\":1,\"height\":1,\"start_col\":0,\"start_row\":0}],\"url_image\":\"g\"}}" +
                "]}]}";

            var content = MessageContent.Parse(raw);
            Assert.NotNull(content.Components);
            var row = Assert.Single(content.Components!);
            Assert.Equal(7, row.Components.Count);
            Assert.IsType<ButtonMessageComponent>(row.Components[0]);
            Assert.True(((ButtonMessageComponent)row.Components[0]).Disable);
            Assert.IsType<SelectMessageComponent>(row.Components[1]);
            Assert.Equal("pick", ((SelectMessageComponent)row.Components[1]).Placeholder);
            Assert.IsType<InputMessageComponent>(row.Components[2]);
            Assert.Equal("i1-component", ((InputMessageComponent)row.Components[2]).NestedComponentId);
            Assert.IsType<DatePickerMessageComponent>(row.Components[3]);
            Assert.IsType<RadioMessageComponent>(row.Components[4]);
            Assert.Equal(2, ((RadioMessageComponent)row.Components[4]).MaxOptions);
            Assert.IsType<AnimationMessageComponent>(row.Components[5]);
            Assert.Equal(new[] { "a", "b" }, ((AnimationMessageComponent)row.Components[5]).Pool);
            Assert.IsType<GridMessageComponent>(row.Components[6]);
            Assert.Equal(2, ((GridMessageComponent)row.Components[6]).Columns);
            Assert.Equal(raw, content.ToJson());
        }
    }
}
