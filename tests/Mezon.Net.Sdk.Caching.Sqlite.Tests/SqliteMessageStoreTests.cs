using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Mezon.Net.Sdk.Caching.Sqlite.Tests
{
    public sealed class SqliteMessageStoreTests
    {
        [Fact]
        public async Task OpenAsync_enables_wal_and_applies_migrations()
        {
            var path = CreateTempDatabasePath();
            await using var store = await SqliteMessageStore.OpenAsync(path);
            await store.FlushAsync();

            using var connection = new SqliteConnection($"Data Source={path}");
            await connection.OpenAsync();

            await using (var journalMode = connection.CreateCommand())
            {
                journalMode.CommandText = "PRAGMA journal_mode;";
                var mode = (await journalMode.ExecuteScalarAsync())!.ToString();
                Assert.Equal("wal", mode, ignoreCase: true);
            }

            await using (var tableCheck = connection.CreateCommand())
            {
                tableCheck.CommandText =
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'messages';";
                Assert.Equal(1L, await tableCheck.ExecuteScalarAsync());
            }
        }

        [Fact]
        public async Task UpsertMessageAsync_persists_and_reads_back()
        {
            var path = CreateTempDatabasePath();
            await using var store = await SqliteMessageStore.OpenAsync(path);

            var snapshot = new MessageSnapshot
            {
                MessageId = 100,
                ChannelId = 10,
                ClanId = 1,
                SenderId = 42,
                Content = "{\"t\":\"hello\"}",
                MentionsJson = "[]",
                AttachmentsJson = "[]",
                ReactionsJson = "[{\"emoji_id\":1}]",
                ReferencesJson = "[]",
                TopicId = 5,
                CreateTimeSeconds = 1_700_000_000,
            };

            await store.UpsertMessageAsync(snapshot, revision: 1);
            await store.FlushAsync();

            var loaded = await store.TryGetMessageAsync(10, 100);
            Assert.NotNull(loaded);
            Assert.Equal(snapshot.Content, loaded!.Content);
            Assert.Equal(snapshot.SenderId, loaded.SenderId);
            Assert.Equal(snapshot.ReactionsJson, loaded.ReactionsJson);
            Assert.Equal(snapshot.TopicId, loaded.TopicId);
        }

        [Fact]
        public async Task UpsertMessageAsync_ignores_stale_revision()
        {
            var path = CreateTempDatabasePath();
            await using var store = await SqliteMessageStore.OpenAsync(path);

            var initial = new MessageSnapshot
            {
                MessageId = 100,
                ChannelId = 10,
                ClanId = 1,
                Content = "first",
            };
            var updated = new MessageSnapshot
            {
                MessageId = 100,
                ChannelId = 10,
                ClanId = 1,
                Content = "second",
            };

            await store.UpsertMessageAsync(initial, revision: 5);
            await store.UpsertMessageAsync(updated, revision: 3);
            await store.FlushAsync();

            var loaded = await store.TryGetMessageAsync(10, 100);
            Assert.Equal("first", loaded!.Content);
        }

        [Fact]
        public async Task DeleteMessageAsync_removes_row_when_revision_is_current()
        {
            var path = CreateTempDatabasePath();
            await using var store = await SqliteMessageStore.OpenAsync(path);

            await store.UpsertMessageAsync(new MessageSnapshot
            {
                MessageId = 100,
                ChannelId = 10,
                ClanId = 1,
                Content = "delete-me",
            }, revision: 1);

            await store.DeleteMessageAsync(10, 100, revision: 2);
            await store.FlushAsync();

            var loaded = await store.TryGetMessageAsync(10, 100);
            Assert.Null(loaded);
        }

        [Fact]
        public async Task ApplyReactionAsync_updates_reactions_json()
        {
            var path = CreateTempDatabasePath();
            await using var store = await SqliteMessageStore.OpenAsync(path);

            await store.UpsertMessageAsync(new MessageSnapshot
            {
                MessageId = 100,
                ChannelId = 10,
                ClanId = 1,
                ReactionsJson = "[]",
            }, revision: 1);

            await store.ApplyReactionAsync(10, 100, "[{\"emoji_id\":9,\"count\":2}]", revision: 2);
            await store.FlushAsync();

            var loaded = await store.TryGetMessageAsync(10, 100);
            Assert.Equal("[{\"emoji_id\":9,\"count\":2}]", loaded!.ReactionsJson);
        }

        [Fact]
        public async Task PruneAsync_removes_messages_older_than_retention()
        {
            var path = CreateTempDatabasePath();
            await using var store = await SqliteMessageStore.OpenAsync(path);

            var oldTimestamp = DateTimeOffset.UtcNow.AddDays(-40).ToUnixTimeSeconds();
            await store.UpsertMessageAsync(new MessageSnapshot
            {
                MessageId = 1,
                ChannelId = 10,
                ClanId = 1,
                Content = "old",
                CreateTimeSeconds = oldTimestamp,
            }, revision: 1);

            await store.UpsertMessageAsync(new MessageSnapshot
            {
                MessageId = 2,
                ChannelId = 10,
                ClanId = 1,
                Content = "fresh",
                CreateTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            }, revision: 1);

            await store.PruneAsync(TimeSpan.FromDays(30));
            await store.FlushAsync();

            Assert.Null(await store.TryGetMessageAsync(10, 1));
            Assert.NotNull(await store.TryGetMessageAsync(10, 2));
        }

        [Fact]
        public async Task ClearAsync_removes_all_messages()
        {
            var path = CreateTempDatabasePath();
            await using var store = await SqliteMessageStore.OpenAsync(path);

            await store.UpsertMessageAsync(new MessageSnapshot
            {
                MessageId = 100,
                ChannelId = 10,
                ClanId = 1,
                Content = "x",
            }, revision: 1);

            await store.ClearAsync();
            await store.FlushAsync();

            Assert.Null(await store.TryGetMessageAsync(10, 100));
        }

        [Fact]
        public void ResolveDatabasePath_sanitizes_account_and_environment()
        {
            var path = SqliteMessageStorePaths.ResolveDatabasePath(@"C:\cache", "bot/123", "prod:main");
            Assert.Equal(Path.Combine(@"C:\cache", "bot_123_prod_main.db"), path);
        }

        private static string CreateTempDatabasePath()
        {
            var directory = Path.Combine(Path.GetTempPath(), "mezon-sqlite-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "messages.db");
        }
    }
}
