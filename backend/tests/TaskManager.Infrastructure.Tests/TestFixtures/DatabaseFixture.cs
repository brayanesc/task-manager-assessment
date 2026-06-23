using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Infrastructure.Tests.TestFixtures;

/// <summary>
/// Creates an isolated SQLite file database for one test class.
/// Using a temp file (rather than shared-cache in-memory) lets UnitOfWork enable
/// WAL mode without contention, which is required now that CommitAsync() keeps an
/// open transaction alive for the lifetime of the UnitOfWork scope.
/// Each fixture instance owns its own file so test classes cannot interfere.
/// </summary>
public sealed class DatabaseFixture : IAsyncDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"taskmanager_infra_{Guid.NewGuid():N}.db");

    public string ConnectionString => $"Data Source={_dbPath}";

    public DatabaseFixture()
    {
        var initializer = new DatabaseInitializer(ConnectionString);
        initializer.InitializeAsync().GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync()
    {
        try { File.Delete(_dbPath); } catch { /* best-effort */ }
        return ValueTask.CompletedTask;
    }
}
