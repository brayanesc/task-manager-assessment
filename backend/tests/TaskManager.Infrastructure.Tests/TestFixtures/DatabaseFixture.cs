using Microsoft.Data.Sqlite;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Infrastructure.Tests.TestFixtures;

/// <summary>
/// Creates a named shared-cache in-memory SQLite database for one test class.
/// The keep-alive connection prevents SQLite from dropping the in-memory database
/// between individual test method invocations.
/// </summary>
public sealed class DatabaseFixture : IAsyncDisposable
{
    private readonly SqliteConnection _keepAlive;

    public string ConnectionString { get; }

    public DatabaseFixture()
    {
        // Unique DB name per fixture instance so test classes are fully isolated.
        var dbName = $"test_{Guid.NewGuid():N}";
        ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();

        // Bootstrap schema + seed synchronously in the constructor.
        var initializer = new DatabaseInitializer(ConnectionString);
        initializer.InitializeAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync() => await _keepAlive.DisposeAsync();
}
