using System.Threading.Tasks;
using Mezon.Net.Core;
using Mezon.Net.Core.Entities;
using Mezon.Net.Internal.Api;
using Mezon.Net.Models;
using Mezon.Net.Sdk.Caching;

namespace Mezon.Net.Sdk.Entities
{
    public sealed class Clan : IClan
    {
        private readonly MezonClient _client;
        private ClanDesc _desc;
        private EntityCacheView<Channel>? _channelsView;
        private EntityCacheView<Role>? _rolesView;

        internal Clan(MezonClient client, ClanDesc desc)
        {
            _client = client;
            _desc = desc;
        }

        public long Id => _desc.ClanId;
        public string? Name => _desc.ClanName;
        public string? ClanName => _desc.ClanName;
        public long CreatorId => _desc.CreatorId;
        public long WelcomeChannelId => _desc.WelcomeChannelId;

        public EntityCacheView<Channel> Channels =>
            _channelsView ??= new EntityCacheView<Channel>(_client.Channels, channel => channel.ClanId == Id);

        public EntityCacheView<Role> Roles =>
            _rolesView ??= new EntityCacheView<Role>(_client.Roles, role => role.ClanId == Id);

        internal void UpdateFrom(ClanDesc desc) => _desc = desc;

        public async Task<ChannelDescListResponse> LoadChannelsAsync(int? channelType = null, RequestOptions? options = null)
        {
            var list = await _client.ListChannelDescsAsync(
                new ListChannelDescsParams(clanId: Id, channelType: channelType),
                options).ConfigureAwait(false);

            for (var i = 0; i < list.Channeldesc.Count; i++)
            {
                _client.UpsertChannelFromDescription(list.Channeldesc[i].Proto, this);
            }

            return list;
        }

        public Task<Mezon.Net.Models.RoleListEventResponse> ListRolesAsync(int? limit = null, int? state = null, string? cursor = null, RequestOptions? options = null)
            => _client.ListRolesAsync(new RoleListEventParams(clanId: Id, limit: limit, state: state, cursor: cursor), options);

        public Task<Mezon.Net.Models.RoleUserListResponse> ListRoleUsersAsync(long roleId, int? limit = null, string? cursor = null, RequestOptions? options = null)
            => _client.ListRoleUsersAsync(new ListRoleUsersParams(roleId: roleId, limit: limit, cursor: cursor), options);

        public Task<Mezon.Net.Models.RoleResponse> CreateRoleAsync(CreateRoleParams body, RequestOptions? options = null)
        {
            var withClan = new CreateRoleParams(
                title: body.Title,
                color: body.Color,
                roleIcon: body.RoleIcon,
                description: body.Description,
                clanId: body.ClanId ?? Id,
                displayOnline: body.DisplayOnline,
                allowMention: body.AllowMention,
                maxPermissionId: body.MaxPermissionId,
                addUserIds: body.AddUserIds,
                activePermissionIds: body.ActivePermissionIds,
                orderRole: body.OrderRole);
            return _client.CreateRoleAsync(withClan, options);
        }

        public Task UpdateRoleAsync(UpdateRoleParams body, RequestOptions? options = null)
        {
            var withClan = new UpdateRoleParams(
                roleId: body.RoleId,
                title: body.Title,
                color: body.Color,
                roleIcon: body.RoleIcon,
                description: body.Description,
                displayOnline: body.DisplayOnline,
                allowMention: body.AllowMention,
                addUserIds: body.AddUserIds,
                activePermissionIds: body.ActivePermissionIds,
                removeUserIds: body.RemoveUserIds,
                removePermissionIds: body.RemovePermissionIds,
                clanId: body.ClanId ?? Id,
                maxPermissionId: body.MaxPermissionId);
            return _client.UpdateRoleAsync(withClan, options);
        }

        public Task<Mezon.Net.Models.RoleListResponse> GetRoleOfUserInTheClanAsync(RequestOptions? options = null)
            => _client.GetRoleOfUserInTheClanAsync(Id, options);

        public Task<VoiceChannelUserListResponse> ListChannelVoiceUsersAsync(long channelId, int channelType, RequestOptions? options = null)
            => _client.ListChannelVoiceUsersAsync(Id, channelId, channelType, options);

        public Task<ChannelDescriptionResponse> CreateChannelDescAsync(CreateChannelDescParams body, RequestOptions? options = null)
        {
            var withClan = new CreateChannelDescParams(
                clanId: body.ClanId ?? Id,
                parentId: body.ParentId,
                channelId: body.ChannelId,
                categoryId: body.CategoryId,
                type: body.Type,
                channelLabel: body.ChannelLabel,
                channelPrivate: body.ChannelPrivate,
                userIds: body.UserIds,
                appId: body.AppId);
            return _client.CreateChannelDescAsync(withClan, options);
        }

        public MezonClient GetClient() => _client;
    }
}
