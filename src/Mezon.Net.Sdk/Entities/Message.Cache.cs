using Google.Protobuf;
using Mezon.Net.Internal.Api;
using Mezon.Net.Models;

namespace Mezon.Net.Sdk.Entities
{
    public sealed partial class Message
    {
        internal void UpdateFrom(ChannelMessageResponse source)
        {
            _source = source;
            _content = null;
        }

        internal void UpdateFrom(ChannelMessageUpdateResponse update)
        {
            var proto = _source?.Proto ?? new ChannelMessage
            {
                ClanId = update.ClanId,
                ChannelId = update.ChannelId,
                MessageId = update.MessageId,
            };

            var updateProto = update.Proto;
            proto.Content = updateProto.Content;
            proto.HideEditted = updateProto.HideEditted;
            proto.TopicId = updateProto.TopicId;
            proto.IsPublic = updateProto.IsPublic;
            proto.Mode = updateProto.Mode;
            if (updateProto.CreateTimeSeconds != 0)
            {
                proto.CreateTimeSeconds = updateProto.CreateTimeSeconds;
            }

            if (updateProto.Mentions.Count > 0)
            {
                var mentions = new MessageMentionList();
                mentions.Mentions.AddRange(updateProto.Mentions);
                proto.Mentions = mentions.ToByteString();
            }

            if (updateProto.Attachments.Count > 0)
            {
                var attachments = new MessageAttachmentList();
                attachments.Attachments.AddRange(updateProto.Attachments);
                proto.Attachments = attachments.ToByteString();
            }

            _source = ChannelMessageResponse.Decode(proto);
            _content = null;
        }

        internal void ApplyReaction(MessageReactionResponse reaction)
        {
            if (_source == null)
            {
                return;
            }

            var proto = _source.Value.Proto;
            var list = DecodeReactionList(proto.Reactions);

            if (reaction.Action)
            {
                for (var i = list.Reactions.Count - 1; i >= 0; i--)
                {
                    var existing = list.Reactions[i];
                    if (existing.EmojiId == reaction.EmojiId && existing.SenderId == reaction.SenderId)
                    {
                        list.Reactions.RemoveAt(i);
                        continue;
                    }

                    if (existing.EmojiId == reaction.EmojiId)
                    {
                        existing.Count = reaction.Count;
                        if (existing.Count <= 0)
                        {
                            list.Reactions.RemoveAt(i);
                        }
                    }
                }
            }
            else if (TryFindReactionIndex(list, reaction.EmojiId, out var index))
            {
                list.Reactions[index].Count = reaction.Count;
            }
            else
            {
                list.Reactions.Add(reaction.Proto.Clone());
            }

            proto.Reactions = list.ToByteString();
            _source = ChannelMessageResponse.Decode(proto);
            _content = null;
        }

        private static MessageReactionList DecodeReactionList(ByteString bytes)
        {
            if (bytes.IsEmpty)
            {
                return new MessageReactionList();
            }

            try
            {
                return MessageReactionList.Parser.ParseFrom(bytes);
            }
            catch
            {
                return new MessageReactionList();
            }
        }

        private static bool TryFindReactionIndex(MessageReactionList list, long emojiId, out int index)
        {
            for (var i = 0; i < list.Reactions.Count; i++)
            {
                if (list.Reactions[i].EmojiId == emojiId)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }
    }
}
