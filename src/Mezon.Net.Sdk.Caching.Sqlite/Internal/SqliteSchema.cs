using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Mezon.Net.Sdk.Caching.Sqlite.Internal
{
    internal static class SqliteSchema
    {
        internal const int LatestVersion = 1;

        internal static void ApplyMigrations(SqliteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS __schema_version (
                      version INTEGER NOT NULL PRIMARY KEY
                    );
                    """;
                command.ExecuteNonQuery();
            }

            var currentVersion = GetCurrentVersion(connection);
            foreach (var migration in EnumerateMigrations())
            {
                if (migration.Version <= currentVersion)
                {
                    continue;
                }

                using var transaction = connection.BeginTransaction();
                migration.Apply(connection, transaction);
                using (var mark = connection.CreateCommand())
                {
                    mark.Transaction = transaction;
                    mark.CommandText = "INSERT INTO __schema_version(version) VALUES ($version);";
                    mark.Parameters.AddWithValue("$version", migration.Version);
                    mark.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        private static int GetCurrentVersion(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT IFNULL(MAX(version), 0) FROM __schema_version;";
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private static IEnumerable<Migration> EnumerateMigrations()
        {
            yield return new Migration(
                1,
                """
                CREATE TABLE IF NOT EXISTS messages (
                  channel_id INTEGER NOT NULL,
                  message_id INTEGER NOT NULL,
                  clan_id INTEGER NOT NULL,
                  sender_id INTEGER NOT NULL DEFAULT 0,
                  content TEXT NOT NULL DEFAULT '',
                  mentions_json TEXT NOT NULL DEFAULT '[]',
                  attachments_json TEXT NOT NULL DEFAULT '[]',
                  reactions_json TEXT NOT NULL DEFAULT '[]',
                  references_json TEXT NOT NULL DEFAULT '[]',
                  topic_id INTEGER,
                  create_time_seconds INTEGER,
                  revision INTEGER NOT NULL,
                  updated_at INTEGER NOT NULL,
                  PRIMARY KEY (channel_id, message_id)
                );

                CREATE INDEX IF NOT EXISTS idx_messages_channel
                  ON messages(channel_id);

                CREATE INDEX IF NOT EXISTS idx_messages_create_time
                  ON messages(create_time_seconds);

                CREATE INDEX IF NOT EXISTS idx_messages_updated_at
                  ON messages(updated_at);
                """);
        }

        private sealed class Migration
        {
            private readonly string _sql;

            internal Migration(int version, string sql)
            {
                Version = version;
                _sql = sql;
            }

            internal int Version { get; }

            internal void Apply(SqliteConnection connection, SqliteTransaction transaction)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = _sql;
                command.ExecuteNonQuery();
            }
        }
    }
}
