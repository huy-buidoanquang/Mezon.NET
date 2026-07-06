namespace Mezon.Net.Core.Entities
{
    public interface IUser : IEntity<long>
    {
        string? Username { get; }
        string? DisplayName { get; }
        string? ClanNick { get; }
        long? DmChannelId { get; }
    }
}
