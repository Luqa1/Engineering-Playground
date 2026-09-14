using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace EngineeringPlayground.DistributedLock.Infrastructure;

public sealed class PostgresAdvisoryLock(NpgsqlDataSource dataSource)
{
    public async Task<PostgresAdvisoryLockLease?> TryAcquireAsync(
        string jobName,
        string executionKey,
        CancellationToken cancellationToken = default)
    {
        var lockKey = PostgresAdvisoryLockKey.Create(jobName, executionKey);
        var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT pg_try_advisory_lock(@lockKey);",
                connection);
            command.Parameters.AddWithValue("lockKey", lockKey);

            var acquired = await command.ExecuteScalarAsync(cancellationToken) is true;
            if (!acquired)
            {
                await connection.DisposeAsync();
                return null;
            }

            return new PostgresAdvisoryLockLease(connection, lockKey);
        }
        catch
        {
            NpgsqlConnection.ClearPool(connection);
            await connection.DisposeAsync();
            throw;
        }
    }
}

public sealed class PostgresAdvisoryLockLease : IAsyncDisposable
{
    private readonly NpgsqlConnection connection;
    private readonly long lockKey;
    private int released;

    internal PostgresAdvisoryLockLease(NpgsqlConnection connection, long lockKey)
    {
        this.connection = connection;
        this.lockKey = lockKey;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref released, 1) != 0)
        {
            return;
        }

        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT pg_advisory_unlock(@lockKey);",
                connection);
            command.Parameters.AddWithValue("lockKey", lockKey);

            bool wasReleased;
            try
            {
                wasReleased = await command.ExecuteScalarAsync() is true;
            }
            catch
            {
                NpgsqlConnection.ClearPool(connection);
                throw;
            }

            if (!wasReleased)
            {
                throw new InvalidOperationException(
                    "The PostgreSQL session did not own the advisory lock being released.");
            }
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}

public static class PostgresAdvisoryLockKey
{
    public static long Create(string jobName, string executionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionKey);

        var identity = Encoding.UTF8.GetBytes(string.Concat(jobName, "\0", executionKey));
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(identity, hash);

        return BinaryPrimitives.ReadInt64BigEndian(hash);
    }
}
