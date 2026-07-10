using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Core.Entities;
using Mezon.Net.Internal.Api;

using Mezon.Net.Sdk.Caching;

namespace Mezon.Net.Sdk.Entities
{
    public sealed class Clan : IClan
    {
        private readonly MezonClient _client;
        private ClanDesc _desc;

        internal Clan(MezonClient client, ClanDesc desc)
        {
            _client = client;
            _desc = desc;
        }

        public long Id => _desc.ClanId;
        public string? Name => _desc.ClanName;
        public string? ClanName => _desc.ClanName;
        public long WelcomeChannelId => _desc.WelcomeChannelId;

        public EntityCache<TextChannel> Channels => _client.Channels;

        internal void UpdateFrom(ClanDesc desc) => _desc = desc;

        public Task<ChannelDescList> LoadChannelsAsync(int? channelType = null, RequestOptions? options = null)
            => _client.Engine.ListChannelDescsAsync(Id, channelType: channelType, options: options);

        public Task<RoleListEventResponse> ListRolesAsync(int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null)
            => _client.Engine.ListRolesAsync(Id, limit, state, cursor, options);

        public Task UpdateRoleAsync(global::Mezon.Net.Internal.Api.UpdateRoleRequest body, RequestOptions? options = null)
            => _client.Api.UpdateRoleAsync(body, options);

        public MezonClient GetClient() => _client;
    }
}
