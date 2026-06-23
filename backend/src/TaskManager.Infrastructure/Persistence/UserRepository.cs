using System.Data.Common;
using Microsoft.Data.Sqlite;
using TaskManager.Application.Interfaces;
using TaskManager.Domain.Entities;

namespace TaskManager.Infrastructure.Persistence;

internal sealed class UserRepository(SqliteConnection connection, Func<SqliteTransaction?> getTransaction)
    : IUserRepository
{
    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = getTransaction();
        cmd.CommandText = "SELECT id, email, password_hash FROM Users WHERE email = @email COLLATE NOCASE";
        cmd.Parameters.AddWithValue("@email", email);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task CreateAsync(User user, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = getTransaction();
        cmd.CommandText = "INSERT INTO Users (id, email, password_hash) VALUES (@id, @email, @hash)";
        cmd.Parameters.AddWithValue("@id", user.Id.ToString());
        cmd.Parameters.AddWithValue("@email", user.Email);
        cmd.Parameters.AddWithValue("@hash", user.PasswordHash);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> ExistsAsync(string email, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = getTransaction();
        cmd.CommandText = "SELECT COUNT(*) FROM Users WHERE email = @email COLLATE NOCASE";
        cmd.Parameters.AddWithValue("@email", email);
        var count = (long)(await cmd.ExecuteScalarAsync(ct))!;
        return count > 0;
    }

    private static User Map(DbDataReader r) =>
        User.Reconstitute(
            Guid.Parse(r.GetString(r.GetOrdinal("id"))),
            r.GetString(r.GetOrdinal("email")),
            r.GetString(r.GetOrdinal("password_hash"))
        );
}
