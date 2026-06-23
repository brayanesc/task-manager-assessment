using Microsoft.Data.Sqlite;
using TaskManager.Infrastructure.Auth;

namespace TaskManager.Infrastructure.Persistence;

public sealed class DatabaseInitializer(string connectionString)
{
    // Fixed IDs ensure INSERT OR IGNORE is idempotent across restarts.
    private static readonly string SeedUserId = "00000000-0000-0000-0000-000000000001";
    private static readonly string SeedTask1Id = "00000000-0000-0000-0000-000000000101";
    private static readonly string SeedTask2Id = "00000000-0000-0000-0000-000000000102";
    private static readonly string SeedTask3Id = "00000000-0000-0000-0000-000000000103";

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);

        await ApplyWalPragmaAsync(conn, ct);
        await CreateSchemaAsync(conn, ct);
        await MigrateAsync(conn, ct);
        await SeedAsync(conn, ct);
    }

    private static async Task ApplyWalPragmaAsync(SqliteConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL;";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task CreateSchemaAsync(SqliteConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Users (
                id            TEXT NOT NULL PRIMARY KEY,
                email         TEXT NOT NULL COLLATE NOCASE,
                password_hash TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email ON Users(email COLLATE NOCASE);

            CREATE TABLE IF NOT EXISTS Tasks (
                id          TEXT NOT NULL PRIMARY KEY,
                title       TEXT NOT NULL,
                description TEXT NOT NULL DEFAULT '',
                status      TEXT NOT NULL DEFAULT 'Todo',
                due_date    TEXT NOT NULL,
                user_id     TEXT NOT NULL REFERENCES Users(id) ON DELETE CASCADE,
                priority    TEXT NOT NULL DEFAULT 'Medium',
                created_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                updated_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            );
            CREATE INDEX IF NOT EXISTS idx_tasks_user_id ON Tasks(user_id);
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Idempotent migrations for databases created before schema additions.
    /// Each ALTER TABLE is wrapped in a try/catch — SQLite raises an error
    /// if the column already exists, which we safely ignore.
    /// </summary>
    private static async Task MigrateAsync(SqliteConnection conn, CancellationToken ct)
    {
        try
        {
            await using var cmd = conn.CreateCommand();
            // Existing rows receive the oldest possible timestamp so they sort to the bottom.
            cmd.CommandText = """
                ALTER TABLE Tasks
                ADD COLUMN created_at TEXT NOT NULL DEFAULT '1900-01-01T00:00:00.000Z'
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (SqliteException ex) when (ex.Message.Contains("duplicate column name"))
        {
            // Column already present — nothing to do.
        }

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                ALTER TABLE Tasks
                ADD COLUMN updated_at TEXT NOT NULL DEFAULT '1900-01-01T00:00:00.000Z'
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (SqliteException ex) when (ex.Message.Contains("duplicate column name"))
        {
            // Column already present — nothing to do.
        }

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                ALTER TABLE Tasks
                ADD COLUMN priority TEXT NOT NULL DEFAULT 'Medium'
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (SqliteException ex) when (ex.Message.Contains("duplicate column name"))
        {
            // Column already present — nothing to do.
        }

        // Ensure indexes exist regardless of whether ALTER TABLE ran.
        await using var idxCmd = conn.CreateCommand();
        idxCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_tasks_created_at ON Tasks(created_at)";
        await idxCmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task SeedAsync(SqliteConnection conn, CancellationToken ct)
    {
        var hasher = new PasswordHasherService();
        var passwordHash = hasher.Hash("Demo1234!");

        await using var userCmd = conn.CreateCommand();
        userCmd.CommandText = """
            INSERT OR IGNORE INTO Users (id, email, password_hash)
            VALUES (@id, @email, @hash)
            """;
        userCmd.Parameters.AddWithValue("@id", SeedUserId);
        userCmd.Parameters.AddWithValue("@email", "demo@taskmanager.com");
        userCmd.Parameters.AddWithValue("@hash", passwordHash);
        await userCmd.ExecuteNonQueryAsync(ct);

        await using var taskCmd = conn.CreateCommand();
        taskCmd.CommandText = """
            INSERT OR IGNORE INTO Tasks (id, title, description, status, due_date, user_id, priority) VALUES
                (@t1id, 'Set up project repository', 'Create the monorepo with backend and frontend folders', 'Done',    '2099-01-01', @uid, 'Medium'),
                (@t2id, 'Implement authentication',  'JWT login and registration endpoints',                  'InProgress','2099-06-01', @uid, 'High'),
                (@t3id, 'Write integration tests',   'Cover repositories with real SQLite tests',             'Todo',      '2099-12-01', @uid, 'Medium')
            """;
        taskCmd.Parameters.AddWithValue("@t1id", SeedTask1Id);
        taskCmd.Parameters.AddWithValue("@t2id", SeedTask2Id);
        taskCmd.Parameters.AddWithValue("@t3id", SeedTask3Id);
        taskCmd.Parameters.AddWithValue("@uid", SeedUserId);
        await taskCmd.ExecuteNonQueryAsync(ct);
    }
}
