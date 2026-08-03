namespace Mezon.Net.Core.Entities
{
    public interface IChannel : IEntity<long>
    {
        long ClanId { get; }
        int Type { get; }
        bool IsPrivate { get; }
        string? Name { get; }
    }
}
