namespace Mezon.Net.Models
{
    /// <summary>
    /// Input for sending a quick menu event via realtime envelope.
    /// </summary>
    public readonly struct QuickMenuDataEventParams
    {
        public string MenuName { get; }
        public SendChannelMessageParams Message { get; }
        public long? MessageId { get; }
        public long? MessageSenderId { get; }

        public QuickMenuDataEventParams(
            string menuName,
            SendChannelMessageParams message,
            long? messageId = null,
            long? messageSenderId = null)
        {
            MenuName = menuName;
            Message = message;
            MessageId = messageId;
            MessageSenderId = messageSenderId;
        }
    }
}
