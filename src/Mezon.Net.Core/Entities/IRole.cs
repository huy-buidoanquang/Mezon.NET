namespace Mezon.Net.Core.Entities
{
    public interface IRole : IEntity<long>
    {
        long ClanId { get; }
        string? Title { get; }
        string? Color { get; }
    }
}
