namespace Mezon.Net.Core.Entities
{
    public interface IChannel : IEntity<long>
    {
        long ClanId { get; }
        long ParentId { get; }
        long CategoryId { get; }
        int Type { get; }
        bool IsPrivate { get; }
        string? Name { get; }
    }
}
