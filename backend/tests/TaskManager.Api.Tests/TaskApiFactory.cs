using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TaskManager.Api.Tests;

/// <summary>
/// Spins up the full ASP.NET Core application in-process with an isolated
/// SQLite file database.  Each factory instance owns its own DB file so test
/// classes that each use IClassFixture&lt;TaskApiFactory&gt; cannot interfere.
/// </summary>
public sealed class TaskApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"taskmanager_test_{Guid.NewGuid():N}.db");

    // Expose the JWT key so helper methods can build test tokens if needed.
    public const string TestJwtKey = "integration-test-secret-key-that-is-long-enough-32+";
    public const string TestJwtIssuer = "taskmanager-test";
    public const string TestJwtAudience = "taskmanager-test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={_dbPath}",
                ["Jwt:Key"]      = TestJwtKey,
                ["Jwt:Issuer"]   = TestJwtIssuer,
                ["Jwt:Audience"] = TestJwtAudience,
                ["Jwt:ExpiryMinutes"] = "60"
            });
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); }
            catch { /* best-effort cleanup */ }
        }
    }
}
