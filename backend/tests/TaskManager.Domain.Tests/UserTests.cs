using TaskManager.Domain.Entities;
using TaskManager.Domain.Exceptions;

namespace TaskManager.Domain.Tests;

public class UserTests
{
    // ── Create: happy path ────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidInput_SetsAllFields()
    {
        var user = User.Create("alice@example.com", "hashed_password");

        Assert.Equal("alice@example.com", user.Email);
        Assert.Equal("hashed_password", user.PasswordHash);
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Fact]
    public void Create_NormalizesEmailToLowercase()
    {
        var user = User.Create("ALICE@EXAMPLE.COM", "hash");
        Assert.Equal("alice@example.com", user.Email);
    }

    [Fact]
    public void Create_TwoUsers_HaveDistinctIds()
    {
        var a = User.Create("a@example.com", "hash");
        var b = User.Create("b@example.com", "hash");
        Assert.NotEqual(a.Id, b.Id);
    }

    // ── Create: email validation ──────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyOrWhitespaceEmail_ThrowsDomainException(string email)
    {
        var ex = Assert.Throws<DomainException>(() => User.Create(email, "hash"));
        Assert.Contains("email", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Reconstitute (ADO.NET mapping — bypasses validation) ─────────────────

    [Fact]
    public void Reconstitute_SetsFieldsWithoutValidation()
    {
        var id = Guid.NewGuid();
        var user = User.Reconstitute(id, "alice@example.com", "stored_hash");

        Assert.Equal(id, user.Id);
        Assert.Equal("alice@example.com", user.Email);
        Assert.Equal("stored_hash", user.PasswordHash);
    }
}
