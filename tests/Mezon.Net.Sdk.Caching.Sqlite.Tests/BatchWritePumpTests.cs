using System;
using System.Threading;
using System.Threading.Tasks;
using Mezon.Net.Sdk.Caching.Sqlite.Internal;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Mezon.Net.Sdk.Caching.Sqlite.Tests
{
    public sealed class BatchWritePumpTests
    {
        [Fact]
        public async Task Failed_batch_faults_the_flush_and_the_pump_keeps_writing()
        {
            using var connection = await OpenConnectionAsync();
            await using var pump = new BatchWritePump(connection);

            pump.Enqueue(new FailingWrite());
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => pump.FlushAsync(CancellationToken.None));
            Assert.Equal("write failed", failure.Message);

            pump.Enqueue(new InsertWrite(42));
            await pump.FlushAsync(CancellationToken.None);

            Assert.Equal(0, pump.PendingCount);
            Assert.Equal(1L, await CountRowsAsync(connection));
        }

        [Fact]
        public async Task Dispose_completes_after_a_failed_batch()
        {
            using var connection = await OpenConnectionAsync();
            var pump = new BatchWritePump(connection);
            pump.Enqueue(new FailingWrite());

            var dispose = pump.DisposeAsync().AsTask();

            Assert.Same(dispose, await Task.WhenAny(dispose, Task.Delay(TimeSpan.FromSeconds(5))));
            Assert.Equal(0, pump.PendingCount);
        }

        private static async Task<SqliteConnection> OpenConnectionAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            using var create = connection.CreateCommand();
            create.CommandText = "CREATE TABLE items (id INTEGER PRIMARY KEY);";
            await create.ExecuteNonQueryAsync();
            return connection;
        }

        private static async Task<long> CountRowsAsync(SqliteConnection connection)
        {
            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM items;";
            return (long)(await count.ExecuteScalarAsync())!;
        }

        private sealed class FailingWrite : IWriteOperation
        {
            public void Execute(SqliteConnection connection, SqliteTransaction transaction)
                => throw new InvalidOperationException("write failed");
        }

        private sealed class InsertWrite : IWriteOperation
        {
            private readonly long _id;

            public InsertWrite(long id) => _id = id;

            public void Execute(SqliteConnection connection, SqliteTransaction transaction)
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO items (id) VALUES ($id);";
                insert.Parameters.AddWithValue("$id", _id);
                insert.ExecuteNonQuery();
            }
        }
    }
}
