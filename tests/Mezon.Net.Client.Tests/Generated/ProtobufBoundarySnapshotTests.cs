using System.IO;
using Xunit;

namespace Mezon.Net.Client.Tests.Generated
{
    public sealed class ProtobufBoundarySnapshotTests
    {
        [Fact]
        public void Generated_facade_contains_core_socket_apis()
        {
            var root = FindRepoRoot();
            var facade = File.ReadAllText(Path.Combine(root, "src", "Mezon.Net.Client", "Generated", "BaseSocketClient.Api.g.cs"));
            var iface = File.ReadAllText(Path.Combine(root, "src", "Mezon.Net.Client", "Generated", "IMezonClientApi.g.cs"));

            Assert.Contains("ListClanDescsAsync", facade);
            Assert.Contains("SendChannelMessageAsync", facade);
            Assert.Contains("Task<ChannelMessageAckData> SendChannelMessageAsync", iface);
        }

        [Fact]
        public void Generated_models_include_channel_message_data_view()
        {
            var root = FindRepoRoot();
            var data = File.ReadAllText(Path.Combine(root, "src", "Mezon.Net.Client", "Models", "Responses", "ChannelMessageData.g.cs"));

            Assert.Contains("namespace Mezon.Net.Models", data);
            Assert.Contains("public readonly struct ChannelMessageData", data);
            Assert.Contains("public long MessageId =>", data);
        }

        [Fact]
        public void Generated_params_include_list_clan_desc_mapper()
        {
            var root = FindRepoRoot();
            var mapper = File.ReadAllText(Path.Combine(root, "src", "Mezon.Net.Client", "Models", "Requests", "ListClanDescParamsMapper.g.cs"));

            Assert.Contains("internal static class ListClanDescParamsMapper", mapper);
            Assert.Contains("ToProto(in ListClanDescParams", mapper);
        }

        [Fact]
        public void Generated_realtime_facade_has_21_methods()
        {
            var root = FindRepoRoot();
            var iface = File.ReadAllText(Path.Combine(root, "src", "Mezon.Net.Client", "Generated", "IMezonClientRealtime.g.cs"));
            var facade = File.ReadAllText(Path.Combine(root, "src", "Mezon.Net.Client", "Generated", "BaseSocketClient.Realtime.g.cs"));

            var methodCount = System.Text.RegularExpressions.Regex.Matches(iface, @"\b\w+RtAsync\s*\(").Count;
            Assert.Equal(21, methodCount);
            Assert.Contains("SendChatMessageRtAsync", facade);
            Assert.Contains("LeaveChannelChatRtAsync", facade);
            Assert.Contains("JoinClanChatRtAsync", facade);
        }

        [Fact]
        public void Realtime_params_include_clan_join_mapper()
        {
            var root = FindRepoRoot();
            var mapper = File.ReadAllText(Path.Combine(root, "src", "Mezon.Net.Client", "Models", "Requests", "ClanJoinParamsMapper.g.cs"));

            Assert.Contains("internal static class ClanJoinParamsMapper", mapper);
            Assert.Contains("ToProto(in ClanJoinParams", mapper);
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Mezon.Net.sln")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }
}
