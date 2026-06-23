using Microsoft.Data.Sqlite;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Infrastructure.Tests;

public class DatabaseInitializerTests : IAsyncDisposable
{
    private readonly SqliteConnection _keepAlive;
    private readonly string _cs;

    public DatabaseInitializerTests()
    {
        var dbName = $"init_test_{Guid.NewGuid():N}";
        _cs = $"Data Source={dbName};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(_cs);
        _keepAlive.Open();
    }

    [Fact]
    public async Task InitializeAsync_CreatesTasksAndUsersTable()
    {
        await new DatabaseInitializer(_cs).InitializeAsync();

        await using var conn = new SqliteConnection(_cs);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('Tasks','Users')";
        await using var reader = await cmd.ExecuteReaderAsync();

        var tables = new List<string>();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(reader.GetOrdinal("name")));

        Assert.Contains("Tasks", tables);
        Assert.Contains("Users", tables);
    }

    [Fact]
    public async Task InitializeAsync_CreatesSeedUser()
    {
        await new DatabaseInitializer(_cs).InitializeAsync();

        await using var conn = new SqliteConnection(_cs);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Users WHERE email = 'demo@taskmanager.com'";
        var count = (long)(await cmd.ExecuteScalarAsync())!;

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task InitializeAsync_CreatesSeedTasks()
    {
        await new DatabaseInitializer(_cs).InitializeAsync();

        await using var conn = new SqliteConnection(_cs);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Tasks";
        var count = (long)(await cmd.ExecuteScalarAsync())!;

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent_RunningTwiceDoesNotDuplicateSeed()
    {
        await new DatabaseInitializer(_cs).InitializeAsync();
        await new DatabaseInitializer(_cs).InitializeAsync(); // second run

        await using var conn = new SqliteConnection(_cs);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Users WHERE email = 'demo@taskmanager.com'";
        var count = (long)(await cmd.ExecuteScalarAsync())!;

        Assert.Equal(1, count);
    }

    public async ValueTask DisposeAsync() => await _keepAlive.DisposeAsync();
}
