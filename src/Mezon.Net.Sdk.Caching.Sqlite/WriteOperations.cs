using System;
using Microsoft.Data.Sqlite;
using Mezon.Net.Sdk.Caching.Sqlite.Internal;

namespace Mezon.Net.Sdk.Caching.Sqlite
{
    internal sealed class UpsertMessageOperation : IWriteOperation
    {
        private readonly MessageSnapshot _snapshot;
        private readonly long _revision;
        private readonly long _updatedAt;

        internal UpsertMessageOperation(MessageSnapshot snapshot, long revision, long updatedAt)
        {
            _snapshot = snapshot;
            _revision = revision;
            _updatedAt = updatedAt;
        }

        public void Execute(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO messages (
                  channel_id, message_id, clan_id, sender_id, content,
                  mentions_json, attachments_json, reactions_json, references_json,
                  topic_id, create_time_seconds, revision, updated_at
                ) VALUES (
                  $channelId, $messageId, $clanId, $senderId, $content,
                  $mentionsJson, $attachmentsJson, $reactionsJson, $referencesJson,
                  $topicId, $createTimeSeconds, $revision, $updatedAt
                )
                ON CONFLICT(channel_id, message_id) DO UPDATE SET
                  clan_id = excluded.clan_id,
                  sender_id = excluded.sender_id,
                  content = excluded.content,
                  mentions_json = excluded.mentions_json,
                  attachments_json = excluded.attachments_json,
                  reactions_json = excluded.reactions_json,
                  references_json = excluded.references_json,
                  topic_id = excluded.topic_id,
                  create_time_seconds = excluded.create_time_seconds,
                  revision = excluded.revision,
                  updated_at = excluded.updated_at
                WHERE excluded.revision >= messages.revision;
                """;

            command.Parameters.AddWithValue("$channelId", _snapshot.ChannelId);
            command.Parameters.AddWithValue("$messageId", _snapshot.MessageId);
            command.Parameters.AddWithValue("$clanId", _snapshot.ClanId);
            command.Parameters.AddWithValue("$senderId", _snapshot.SenderId);
            command.Parameters.AddWithValue("$content", _snapshot.Content ?? string.Empty);
            command.Parameters.AddWithValue("$mentionsJson", _snapshot.MentionsJson ?? "[]");
            command.Parameters.AddWithValue("$attachmentsJson", _snapshot.AttachmentsJson ?? "[]");
            command.Parameters.AddWithValue("$reactionsJson", _snapshot.ReactionsJson ?? "[]");
            command.Parameters.AddWithValue("$referencesJson", _snapshot.ReferencesJson ?? "[]");
            command.Parameters.AddWithValue("$topicId", (object?)_snapshot.TopicId ?? DBNull.Value);
            command.Parameters.AddWithValue("$createTimeSeconds", (object?)_snapshot.CreateTimeSeconds ?? DBNull.Value);
            command.Parameters.AddWithValue("$revision", _revision);
            command.Parameters.AddWithValue("$updatedAt", _updatedAt);
            command.ExecuteNonQuery();
        }
    }

    internal sealed class DeleteMessageOperation : IWriteOperation
    {
        private readonly long _channelId;
        private readonly long _messageId;
        private readonly long _revision;

        internal DeleteMessageOperation(long channelId, long messageId, long revision)
        {
            _channelId = channelId;
            _messageId = messageId;
            _revision = revision;
        }

        public void Execute(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                DELETE FROM messages
                WHERE channel_id = $channelId
                  AND message_id = $messageId
                  AND $revision >= revision;
                """;
            command.Parameters.AddWithValue("$channelId", _channelId);
            command.Parameters.AddWithValue("$messageId", _messageId);
            command.Parameters.AddWithValue("$revision", _revision);
            command.ExecuteNonQuery();
        }
    }

    internal sealed class ApplyReactionOperation : IWriteOperation
    {
        private readonly long _channelId;
        private readonly long _messageId;
        private readonly string _reactionsJson;
        private readonly long _revision;
        private readonly long _updatedAt;

        internal ApplyReactionOperation(long channelId, long messageId, string reactionsJson, long revision, long updatedAt)
        {
            _channelId = channelId;
            _messageId = messageId;
            _reactionsJson = reactionsJson;
            _revision = revision;
            _updatedAt = updatedAt;
        }

        public void Execute(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE messages
                SET reactions_json = $reactionsJson,
                    revision = $revision,
                    updated_at = $updatedAt
                WHERE channel_id = $channelId
                  AND message_id = $messageId
                  AND $revision >= revision;
                """;
            command.Parameters.AddWithValue("$reactionsJson", _reactionsJson ?? "[]");
            command.Parameters.AddWithValue("$revision", _revision);
            command.Parameters.AddWithValue("$updatedAt", _updatedAt);
            command.Parameters.AddWithValue("$channelId", _channelId);
            command.Parameters.AddWithValue("$messageId", _messageId);
            command.ExecuteNonQuery();
        }
    }

    internal sealed class PruneMessagesOperation : IWriteOperation
    {
        private readonly long _cutoffSeconds;

        internal PruneMessagesOperation(long cutoffSeconds) => _cutoffSeconds = cutoffSeconds;

        public void Execute(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                DELETE FROM messages
                WHERE (create_time_seconds IS NOT NULL AND create_time_seconds < $cutoff)
                   OR (create_time_seconds IS NULL AND updated_at < $cutoff);
                """;
            command.Parameters.AddWithValue("$cutoff", _cutoffSeconds);
            command.ExecuteNonQuery();
        }
    }

    internal sealed class ClearMessagesOperation : IWriteOperation
    {
        public void Execute(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM messages;";
            command.ExecuteNonQuery();
        }
    }
}
