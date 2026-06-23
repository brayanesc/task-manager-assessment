using TaskManager.Domain.Entities;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Tests.TestFixtures;

namespace TaskManager.Infrastructure.Tests;

public class UserRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly string _cs;

    public UserRepositoryTests(DatabaseFixture fixture)
    {
        _cs = fixture.ConnectionString;
    }

    private static User NewUser(string email = "test@example.com") =>
        User.Create(email, "hashed_password");

    // ── Create / GetByEmail ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ThenGetByEmailAsync_ReturnsUser()
    {
        await using var uow = new UnitOfWork(_cs);
        var user = NewUser($"user_{Guid.NewGuid():N}@example.com");
        await uow.Users.CreateAsync(user);
        await uow.CommitAsync();

        await using var uow2 = new UnitOfWork(_cs);
        var fetched = await uow2.Users.GetByEmailAsync(user.Email);

        Assert.NotNull(fetched);
        Assert.Equal(user.Id, fetched!.Id);
        Assert.Equal(user.Email, fetched.Email);
        Assert.Equal("hashed_password", fetched.PasswordHash);
    }

    [Fact]
    public async Task GetByEmailAsync_WithNonExistentEmail_ReturnsNull()
    {
        await using var uow = new UnitOfWork(_cs);
        var result = await uow.Users.GetByEmailAsync("ghost@example.com");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByEmailAsync_IsCaseInsensitive()
    {
        var email = $"ci_{Guid.NewGuid():N}@example.com";
        await using var uow = new UnitOfWork(_cs);
        await uow.Users.CreateAsync(NewUser(email));
        await uow.CommitAsync();

        await using var uow2 = new UnitOfWork(_cs);
        var fetched = await uow2.Users.GetByEmailAsync(email.ToUpperInvariant());

        Assert.NotNull(fetched);
    }

    // ── ExistsAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ExistsAsync_WithExistingEmail_ReturnsTrue()
    {
        var email = $"exists_{Guid.NewGuid():N}@example.com";
        await using var uow = new UnitOfWork(_cs);
        await uow.Users.CreateAsync(NewUser(email));
        await uow.CommitAsync();

        await using var uow2 = new UnitOfWork(_cs);
        Assert.True(await uow2.Users.ExistsAsync(email));
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistentEmail_ReturnsFalse()
    {
        await using var uow = new UnitOfWork(_cs);
        Assert.False(await uow.Users.ExistsAsync("nobody@example.com"));
    }

    // ── Seed user exists ──────────────────────────────────────────────────────

    [Fact]
    public async Task SeedUser_ExistsAfterInitialization()
    {
        await using var uow = new UnitOfWork(_cs);
        var seed = await uow.Users.GetByEmailAsync("demo@taskmanager.com");
        Assert.NotNull(seed);
    }
}
