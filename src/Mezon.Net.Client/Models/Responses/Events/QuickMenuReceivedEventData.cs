using Mezon.Net.Internal.Realtime;

namespace Mezon.Net.Models
{
    /// <summary>
    /// Payload received when a quick menu event is delivered by realtime.
    /// </summary>
    public readonly struct QuickMenuReceivedEventData
    {
        private readonly string _menuName;
        private readonly ChannelMessageSendResponse _message;
        private readonly bool _hasMessage;
        private readonly long _senderId;
        private readonly long _messageSenderId;

        internal QuickMenuReceivedEventData(QuickMenuDataEvent proto)
        {
            if (proto is null)
            {
                _menuName = string.Empty;
                _message = default;
                _hasMessage = false;
                _senderId = 0;
                _messageSenderId = 0;
                return;
            }

            _menuName = proto.MenuName ?? string.Empty;
            _hasMessage = proto.Message is not null;
            _message = _hasMessage ? new ChannelMessageSendResponse(proto.Message!) : default;
            _senderId = proto.SenderId;
            _messageSenderId = proto.MessageSenderId;
        }

        /// <summary>Registered quick menu name.</summary>
        public string MenuName => _menuName;

        /// <summary>Source message metadata carried by the quick menu event.</summary>
        public ChannelMessageSendResponse Message => _message;

        /// <summary>Whether source message metadata was present in the payload.</summary>
        public bool HasMessage => _hasMessage;

        /// <summary>Realtime sender of the quick menu event.</summary>
        public long SenderId => _senderId;

        /// <summary>User who authored the source message.</summary>
        public long MessageSenderId => _messageSenderId;

        public long MessageId => _hasMessage ? _message.Id : 0;
        public long ClanId => _hasMessage ? _message.ClanId : 0;
        public long ChannelId => _hasMessage ? _message.ChannelId : 0;
    }
}
