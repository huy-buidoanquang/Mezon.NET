using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Google.Protobuf;
using Mezon.Net.Internal.Realtime;

namespace Mezon.Net.Client.Dispatch
{
    /// <summary>
    /// Picks the ordering key of a realtime envelope: its channel id, else its clan id, else 0. The lookup is resolved
    /// once per envelope type from the generated message properties, so new event types are covered automatically.
    /// </summary>
    /// <remarks>
    /// This reads the generated C# properties rather than the protobuf descriptors: building
    /// <c>Envelope.Descriptor</c> keeps about 3.5 MiB of descriptor graph alive for the life of the process. Values
    /// are read through typed property-getter delegates built once per type, so no <see cref="long"/> is boxed on the
    /// receive path.
    /// </remarks>
    internal static class RealtimeLaneKey
    {
        private static readonly string[] ChannelPropertyNames = { "ChannelId", "VoiceChannelId", "StreamingChannelId" };
        private static readonly string[] ClanPropertyNames = { "ClanId" };
        private static readonly Func<Envelope, long> NoKey = static _ => 0;
        private static readonly MethodInfo Int64GetterFactory =
            typeof(RealtimeLaneKey).GetMethod(nameof(CreateInt64Getter), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo MessageGetterFactory =
            typeof(RealtimeLaneKey).GetMethod(nameof(CreateMessageGetter), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly ConcurrentDictionary<Envelope.MessageOneofCase, Func<Envelope, long>> Accessors = new();

        public static long Compute(Envelope envelope)
        {
            var messageCase = envelope.MessageCase;
            if (messageCase == Envelope.MessageOneofCase.None)
            {
                return 0;
            }

            return Accessors.GetOrAdd(messageCase, static oneofCase => BuildAccessor(oneofCase))(envelope);
        }

        private static Func<Envelope, long> BuildAccessor(Envelope.MessageOneofCase messageCase)
        {
            // protoc names each oneof case after the property that holds it.
            var eventProperty = typeof(Envelope).GetProperty(messageCase.ToString(), BindingFlags.Public | BindingFlags.Instance);
            if (eventProperty == null || !typeof(IMessage).IsAssignableFrom(eventProperty.PropertyType))
            {
                return NoKey;
            }

            var key = Find(eventProperty.PropertyType, ChannelPropertyNames) ?? Find(eventProperty.PropertyType, ClanPropertyNames);
            if (key == null)
            {
                return NoKey;
            }

            var payload = MessageGetter(typeof(Envelope), eventProperty);
            return envelope => payload(envelope) is { } message ? key(message) : 0;
        }

        /// <summary>Looks for one of <paramref name="names"/> on the event, then one level down in singular message fields.</summary>
        private static Func<IMessage, long>? Find(Type eventType, string[] names)
        {
            var direct = FindInt64Property(eventType, names);
            if (direct != null)
            {
                return Int64Getter(eventType, direct);
            }

            var nestedProperties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => typeof(IMessage).IsAssignableFrom(property.PropertyType))
                .OrderBy(property => FieldNumber(eventType, property));
            foreach (var nested in nestedProperties)
            {
                var inner = FindInt64Property(nested.PropertyType, names);
                if (inner != null)
                {
                    var child = MessageGetter(eventType, nested);
                    var value = Int64Getter(nested.PropertyType, inner);
                    return message => child(message) is { } nestedMessage ? value(nestedMessage) : 0;
                }
            }

            return null;
        }

        private static PropertyInfo? FindInt64Property(Type type, string[] names)
        {
            foreach (var name in names)
            {
                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.PropertyType == typeof(long))
                {
                    return property;
                }
            }

            return null;
        }

        /// <summary>The protobuf field number, from the <c>{Property}FieldNumber</c> constant protoc emits.</summary>
        private static int FieldNumber(Type type, PropertyInfo property)
            => type.GetField(property.Name + "FieldNumber", BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue() is int number
                ? number
                : int.MaxValue;

        private static Func<IMessage, long> Int64Getter(Type owner, PropertyInfo property)
            => (Func<IMessage, long>)Int64GetterFactory.MakeGenericMethod(owner)
                .Invoke(null, new object[] { property.GetGetMethod()! })!;

        private static Func<IMessage, IMessage?> MessageGetter(Type owner, PropertyInfo property)
            => (Func<IMessage, IMessage?>)MessageGetterFactory.MakeGenericMethod(owner, property.PropertyType)
                .Invoke(null, new object[] { property.GetGetMethod()! })!;

        private static Func<IMessage, long> CreateInt64Getter<TMessage>(MethodInfo getter)
            where TMessage : class, IMessage
        {
            var read = (Func<TMessage, long>)Delegate.CreateDelegate(typeof(Func<TMessage, long>), getter);
            return message => read((TMessage)message);
        }

        private static Func<IMessage, IMessage?> CreateMessageGetter<TMessage, TChild>(MethodInfo getter)
            where TMessage : class, IMessage
            where TChild : class, IMessage
        {
            var read = (Func<TMessage, TChild?>)Delegate.CreateDelegate(typeof(Func<TMessage, TChild?>), getter);
            return message => read((TMessage)message);
        }
    }
}
