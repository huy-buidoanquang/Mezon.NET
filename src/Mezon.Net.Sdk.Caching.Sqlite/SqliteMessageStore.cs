using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Mezon.Net.Sdk.Caching.Sqlite.Internal;

namespace Mezon.Net.Sdk.Caching.Sqlite
{
    /// <summary>
    ///     SQLite-backed persistent message cache. Writes are queued and flushed on a background
    ///     worker so websocket/event handlers stay off the disk hot path.
    /// </summary>
    public sealed class SqliteMessageStore : IAsyncDisposable
    {
        private readonly SqliteConnection _readConnection;
        private readonly BatchWritePump _writePump;
        private readonly string _databasePath;
        private int _disposed;

        private SqliteMessageStore(string databasePath, SqliteConnection writeConnection, SqliteConnection readConnection, BatchWritePump writePump)
        {
            _databasePath = databasePath;
            WriteConnection = writeConnection;
            _readConnection = readConnection;
            _writePump = writePump;
        }

        internal SqliteConnection WriteConnection { get; }

        /// <summary>
        ///     Opens or creates a database at <paramref name="path"/>, enables WAL mode, and applies migrations.
        /// </summary>
        public static async Task<SqliteMessageStore> OpenAsync(string path, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Database path is required.", nameof(path));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var writeConnection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
            }.ToString());

            await writeConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            ConfigureConnection(writeConnection);
            SqliteSchema.ApplyMigrations(writeConnection);

            var readConnection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
            }.ToString());

            await readConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            ConfigureConnection(readConnection);

            var pump = new BatchWritePump(writeConnection);
            return new SqliteMessageStore(path, writeConnection, readConnection, pump);
        }

        /// <summary>
        ///     Queues an upsert that only applies when <paramref name="revision"/> is newer than the stored revision.
        /// </summary>
        public ValueTask UpsertMessageAsync(MessageSnapshot snapshot, long revision, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            _writePump.Enqueue(new UpsertMessageOperation(snapshot, revision, NowUnixSeconds()));
            return default;
        }

        /// <summary>
        ///     Queues a delete that only applies when <paramref name="revision"/> is newer than the stored revision.
        /// </summary>
        public ValueTask DeleteMessageAsync(long channelId, long messageId, long revision, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            _writePump.Enqueue(new DeleteMessageOperation(channelId, messageId, revision));
            return default;
        }

        /// <summary>
        ///     Queues a reaction update with a monotonically increasing revision and serialized reaction list.
        /// </summary>
        public ValueTask ApplyReactionAsync(
            long channelId,
            long messageId,
            string reactionsJson,
            long revision,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            _writePump.Enqueue(new ApplyReactionOperation(channelId, messageId, reactionsJson, revision, NowUnixSeconds()));
            return default;
        }

        /// <summary>
        ///     Queues deletion of messages older than <paramref name="retention"/>.
        /// </summary>
        public ValueTask PruneAsync(TimeSpan retention, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var cutoff = NowUnixSeconds() - (long)retention.TotalSeconds;
            if (cutoff < 0)
            {
                cutoff = 0;
            }

            _writePump.Enqueue(new PruneMessagesOperation(cutoff));
            return default;
        }

        /// <summary>
        ///     Queues removal of all cached messages.
        /// </summary>
        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            _writePump.Enqueue(new ClearMessagesOperation());
            return default;
        }

        /// <summary>
        ///     Reads a cached message. Uses a separate connection so callers can hydrate without blocking writes.
        /// </summary>
        public ValueTask<MessageSnapshot?> TryGetMessageAsync(long channelId, long messageId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<MessageSnapshot?>(TryGetMessageCore(channelId, messageId));
        }

        private MessageSnapshot? TryGetMessageCore(long channelId, long messageId)
        {
            using var command = _readConnection.CreateCommand();
            command.CommandText =
                """
                SELECT clan_id, sender_id, content, mentions_json, attachments_json, reactions_json,
                       references_json, topic_id, create_time_seconds
                FROM messages
                WHERE channel_id = $channelId AND message_id = $messageId
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$channelId", channelId);
            command.Parameters.AddWithValue("$messageId", messageId);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new MessageSnapshot
            {
                ChannelId = channelId,
                MessageId = messageId,
                ClanId = reader.GetInt64(0),
                SenderId = reader.GetInt64(1),
                Content = reader.GetString(2),
                MentionsJson = reader.GetString(3),
                AttachmentsJson = reader.GetString(4),
                ReactionsJson = reader.GetString(5),
                ReferencesJson = reader.GetString(6),
                TopicId = reader.IsDBNull(7) ? null : reader.GetInt64(7),
                CreateTimeSeconds = reader.IsDBNull(8) ? null : reader.GetInt64(8),
            };
        }

        /// <summary>
        ///     Waits until all queued writes are flushed. Intended for shutdown hooks and tests.
        /// </summary>
        public Task FlushAsync(CancellationToken cancellationToken = default)
            => _writePump.FlushAsync(cancellationToken);

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                await _writePump.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
            }

            await _writePump.DisposeAsync().ConfigureAwait(false);

            await WriteConnection.CloseAsync().ConfigureAwait(false);
            await _readConnection.CloseAsync().ConfigureAwait(false);
            WriteConnection.Dispose();
            _readConnection.Dispose();

            CheckpointWal(_databasePath);
        }

        private static void ConfigureConnection(SqliteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA journal_mode=WAL;";
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA synchronous=NORMAL;";
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys=ON;";
                command.ExecuteNonQuery();
            }
        }

        private static void CheckpointWal(string databasePath)
        {
            try
            {
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                }.ToString());
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                command.ExecuteNonQuery();
            }
            catch
            {
            }
        }

        private static long NowUnixSeconds() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(SqliteMessageStore));
            }
        }
    }
}
