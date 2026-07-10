using System;

namespace Mezon.Net.Core
{
    /// <summary>
    ///     The exception that is thrown when a requested Mezon entity cannot be found in the local cache or via the API.
    /// </summary>
    public class MezonEntityNotFoundException : MezonException
    {
        public string EntityType { get; }
        public long EntityId { get; }

        public MezonEntityNotFoundException(string entityType, long entityId)
            : base($"{entityType} {entityId} was not found.")
        {
            EntityType = entityType;
            EntityId = entityId;
        }

        public MezonEntityNotFoundException(string entityType, long entityId, string message)
            : base(message)
        {
            EntityType = entityType;
            EntityId = entityId;
        }
    }
}
