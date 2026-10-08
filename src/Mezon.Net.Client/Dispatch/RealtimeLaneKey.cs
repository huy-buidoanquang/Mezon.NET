using System;
using System.Collections.Concurrent;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Mezon.Net.Internal.Realtime;

namespace Mezon.Net.Client.Dispatch
{
    /// <summary>
    /// Picks the ordering key of a realtime envelope: its channel id, else its clan id, else 0. The field lookup is
    /// resolved once per envelope type from the protobuf descriptors, so new event types are covered automatically.
    /// </summary>
    internal static class RealtimeLaneKey
    {
        private static readonly string[] ChannelFieldNames = { "channel_id", "voice_channel_id", "streaming_channel_id" };
        private static readonly string[] ClanFieldNames = { "clan_id" };
        private static readonly Func<IMessage, long> NoKey = static _ => 0;
        private static readonly ConcurrentDictionary<Envelope.MessageOneofCase, Func<IMessage, long>> Accessors = new();

        public static long Compute(Envelope envelope)
        {
            var messageCase = envelope.MessageCase;
            if (messageCase == Envelope.MessageOneofCase.None)
            {
                return 0;
            }

            var eventField = Envelope.Descriptor.FindFieldByNumber((int)messageCase);
            if (eventField == null || eventField.FieldType != FieldType.Message)
            {
                return 0;
            }

            var accessor = Accessors.GetOrAdd(messageCase, static (_, field) => BuildAccessor(field.MessageType), eventField);
            return eventField.Accessor.GetValue(envelope) is IMessage payload ? accessor(payload) : 0;
        }

        private static Func<IMessage, long> BuildAccessor(MessageDescriptor eventDescriptor)
        {
            return Find(eventDescriptor, ChannelFieldNames)
                ?? Find(eventDescriptor, ClanFieldNames)
                ?? NoKey;
        }

        /// <summary>Looks for one of <paramref name="names"/> on the event, then one level down in singular message fields.</summary>
        private static Func<IMessage, long>? Find(MessageDescriptor eventDescriptor, string[] names)
        {
            var direct = FindInt64Field(eventDescriptor, names);
            if (direct != null)
            {
                return message => ReadInt64(direct, message);
            }

            foreach (var nested in eventDescriptor.Fields.InFieldNumberOrder())
            {
                if (nested.FieldType != FieldType.Message || nested.IsRepeated || nested.IsMap)
                {
                    continue;
                }

                var inner = FindInt64Field(nested.MessageType, names);
                if (inner != null)
                {
                    return message => nested.Accessor.GetValue(message) is IMessage child ? ReadInt64(inner, child) : 0;
                }
            }

            return null;
        }

        private static FieldDescriptor? FindInt64Field(MessageDescriptor descriptor, string[] names)
        {
            foreach (var name in names)
            {
                var field = descriptor.FindFieldByName(name);
                if (field != null && !field.IsRepeated && field.FieldType == FieldType.Int64)
                {
                    return field;
                }
            }

            return null;
        }

        private static long ReadInt64(FieldDescriptor field, IMessage message)
            => field.Accessor.GetValue(message) is long value ? value : 0;
    }
}
