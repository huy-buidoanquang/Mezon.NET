namespace Mezon.Net.Core.Entities
{
    public interface IMessage : IEntity<long>
    {
        long ClanId { get; }
        long ChannelId { get; }
        long SenderId { get; }
        string Content { get; }
        int Code { get; }
    }
}
