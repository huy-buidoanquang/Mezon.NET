using System.Reflection;
using System.Text.RegularExpressions;
using Mezon.Net.Core.Protocol;

namespace Mezon.Net.Client.Tests;

public sealed class ApiNameIndexMapTests
{
    [Fact]
    public void Hot_path_indices_match_mezon_js_ApiNameEnum()
    {
        // Source: mezon-js transport.ts enum ApiNameEnum (HOT PATH section order).
        (string Name, int ExpectedIndex)[] hotPath =
        [
            ("ListChannelDescs", 0),
            ("GetAccount", 1),
            ("ListClanDescs", 2),
            ("ListClanUsers", 3),
            ("ListRoles", 4),
            ("ListEvents", 5),
            ("GetRoleOfUserInTheClan", 6),
            ("GetListPermission", 7),
            ("ListUserPermissionInChannel", 8),
            ("GetNotificationClan", 9),
            ("ListMutedChannel", 10),
            ("ListStreamingChannelUsers", 11),
            ("ListQuickMenuAccess", 12),
            ("GetNotificationChannel", 13),
            ("ListFriends", 14),
            ("EmojiRecentList", 15),
            ("GetListEmojisByUserId", 16),
            ("ListClanBadgeCount", 17),
            ("ListChannelBadgeCount", 18),
            ("ListLogedDevice", 19),
            ("ListClanUsersStatus", 20),
            ("ListChannelApps", 21),
            ("GetListFavoriteChannel", 22),
            ("ListCategoryDescs", 23),
            ("ListOnboarding", 24),
            ("GetListStickersByUserId", 25),
            ("GetSystemMessageByClanId", 26),
            ("GetPinMessagesList", 27),
            ("GetChannelCanvasList", 28),
            ("ListChannelTimeline", 29),
            ("ListChannelMessages", 30),
            ("ListActivity", 31),
            ("ListChannelByUserId", 32),
            ("ListUserClansByUserId", 33),
            ("GetUserProfileOnClan", 34),
            ("RegistFCMDeviceToken", 35),
            ("IsBanned", 36),
            ("ListThreadDescs", 37),
            ("ListArchivedChannelDescs", 38),
            ("ListChannelDetail", 39),
        ];

        foreach (var (name, expectedIndex) in hotPath)
        {
            Assert.True(ApiNameIndexMap.TryGetIndex(name, out var index), $"Missing map entry for {name}");
            Assert.Equal(expectedIndex, index);
        }
    }

    [Fact]
    public void Map_contains_all_expected_api_names()
    {
        Assert.Equal(210, ApiNameIndexMap.NameToIndex.Count);
    }

    [Fact]
    public void CreateActivity_alias_resolves_to_CreateActiviy_index()
    {
        Assert.True(ApiNameIndexMap.TryGetIndex("CreateActivity", out var index));
        Assert.True(ApiNameIndexMap.TryGetIndex("CreateActiviy", out var typoIndex));
        Assert.Equal(typoIndex, index);
    }

    [Fact]
    public void Unknown_api_name_is_not_resolved()
    {
        Assert.False(ApiNameIndexMap.TryGetIndex("DefinitelyMissingApi", out _));
    }

    [Fact]
    public void Socket_overrides_cover_every_api_name_in_map()
    {
        var assembly = typeof(Mezon.Net.Client.MezonClient).Assembly;
        var source = string.Join('\n', assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Select(m => m.DeclaringType?.Name + " " + m.Name));

        var clientSources = Directory.GetFiles(
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Mezon.Net.Client")),
                "MezonSocketApiClient*.cs",
                SearchOption.AllDirectories)
            .Select(File.ReadAllText);

        var implemented = new HashSet<string>(StringComparer.Ordinal);
        foreach (var text in clientSources)
        {
            foreach (Match match in Regex.Matches(text, "SendApiAsync\\(\"([^\"]+)\""))
            {
                implemented.Add(match.Groups[1].Value);
            }
        }

        var missing = ApiNameIndexMap.NameToIndex.Keys.Where(name => !implemented.Contains(name)).OrderBy(x => x).ToArray();
        Assert.True(missing.Length == 0, $"Missing socket API implementations: {string.Join(", ", missing)}");
    }
}
