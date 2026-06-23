using Microsoft.Data.Sqlite;
using TaskManager.Application.Interfaces;

namespace TaskManager.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;

    public ITaskRepository Tasks { get; }
    public IUserRepository Users { get; }

    public UnitOfWork(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        // WAL mode on every new connection — improves read concurrency on file DBs.
        // Silently ignored for :memory: databases.
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();

        _transaction = _connection.BeginTransaction();

        Tasks = new TaskRepository(_connection, _transaction);
        Users = new UserRepository(_connection, _transaction);
    }

    public async Task CommitAsync(CancellationToken ct = default) =>
        await _transaction.CommitAsync(ct);

    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync(); // rolls back if CommitAsync was never called
        await _connection.DisposeAsync();
    }
}
