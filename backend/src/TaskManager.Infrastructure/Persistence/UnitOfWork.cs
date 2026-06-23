using Microsoft.Data.Sqlite;
using TaskManager.Application.Interfaces;

namespace TaskManager.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly SqliteConnection _connection;
    private SqliteTransaction? _transaction;

    public ITaskRepository Tasks { get; }
    public IUserRepository Users { get; }

    public UnitOfWork(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        // WAL mode is set once by DatabaseInitializer and persists on file-based
        // SQLite, so we do NOT re-issue PRAGMA journal_mode=WAL here — doing so
        // while another connection has an open transaction causes "database is locked".
        // We only enable foreign-key enforcement, which is session-level and lock-free.
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();

        _transaction = _connection.BeginTransaction();

        // Repositories capture a delegate so they always reference the current
        // transaction.  After CommitAsync() sets _transaction = null, commands
        // run in SQLite autocommit mode (reads still work; the API's post-commit
        // reads in the Register→Login flow rely on this).
        Tasks = new TaskRepository(_connection, () => _transaction);
        Users = new UserRepository(_connection, () => _transaction);
    }

    /// <summary>
    /// Commits the current transaction.  Subsequent operations on the same
    /// UnitOfWork instance run in autocommit mode, which is sufficient for
    /// the read-only calls that follow a commit within a single request scope.
    /// </summary>
    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync(); // rolls back any uncommitted work
        await _connection.DisposeAsync();
    }
}
