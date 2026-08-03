namespace Mezon.Net.Core.Entities
{
    public interface IClan : IEntity<long>
    {
        string? Name { get; }
        string? ClanName { get; }
        long WelcomeChannelId { get; }
    }
}
