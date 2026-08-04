using System.Collections.Generic;
using Mezon.Net.Core.Entities;

namespace Mezon.Net.Sdk.Entities
{
    public sealed class Role : IRole
    {
        private global::Mezon.Net.Internal.Api.Role _proto;
        private readonly HashSet<long> _memberIds = new HashSet<long>();

        internal Role(global::Mezon.Net.Internal.Api.Role proto)
        {
            _proto = proto;
            SeedMembersFromProto();
        }

        internal Role(long roleId, long clanId)
        {
            _proto = new global::Mezon.Net.Internal.Api.Role
            {
                Id = roleId,
                ClanId = clanId,
            };
        }

        public long Id => _proto.Id;
        public long ClanId => _proto.ClanId;
        public string? Title => string.IsNullOrEmpty(_proto.Title) ? null : _proto.Title;
        public string? Color => string.IsNullOrEmpty(_proto.Color) ? null : _proto.Color;
        public string? RoleIcon => string.IsNullOrEmpty(_proto.RoleIcon) ? null : _proto.RoleIcon;
        public string? Description => string.IsNullOrEmpty(_proto.Description) ? null : _proto.Description;
        public long CreatorId => _proto.CreatorId;
        public int Active => _proto.Active;
        public int DisplayOnline => _proto.DisplayOnline;
        public int AllowMention => _proto.AllowMention;
        public int OrderRole => _proto.OrderRole;
        public int MaxLevelPermission => _proto.MaxLevelPermission;

        /// <summary>Known member user ids from role events / list payloads (may be incomplete).</summary>
        public IReadOnlyCollection<long> MemberIds => _memberIds;

        internal global::Mezon.Net.Internal.Api.Role Proto => _proto;

        internal void UpdateFrom(global::Mezon.Net.Internal.Api.Role proto)
        {
            _proto = proto;
            SeedMembersFromProto();
        }

        internal void ApplyAssigned(IEnumerable<long> userIds)
        {
            foreach (var id in userIds)
            {
                if (id != 0)
                {
                    _memberIds.Add(id);
                }
            }
        }

        internal void ApplyRemoved(IEnumerable<long> userIds)
        {
            foreach (var id in userIds)
            {
                _memberIds.Remove(id);
            }
        }

        private void SeedMembersFromProto()
        {
            if (_proto.RoleUserList?.RoleUsers == null)
            {
                return;
            }

            for (var i = 0; i < _proto.RoleUserList.RoleUsers.Count; i++)
            {
                var userId = _proto.RoleUserList.RoleUsers[i].Id;
                if (userId != 0)
                {
                    _memberIds.Add(userId);
                }
            }
        }
    }
}
