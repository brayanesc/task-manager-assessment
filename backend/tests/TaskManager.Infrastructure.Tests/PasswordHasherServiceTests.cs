using TaskManager.Infrastructure.Auth;

namespace TaskManager.Infrastructure.Tests;

public class PasswordHasherServiceTests
{
    private readonly PasswordHasherService _sut = new();

    [Fact]
    public void Hash_ReturnsDifferentStringFromPlaintext()
    {
        var hash = _sut.Hash("MyPassword1!");
        Assert.NotEqual("MyPassword1!", hash);
    }

    [Fact]
    public void Hash_ProducesNonEmptyResult()
    {
        var hash = _sut.Hash("MyPassword1!");
        Assert.False(string.IsNullOrWhiteSpace(hash));
    }

    [Fact]
    public void Hash_TwoCallsWithSamePassword_ProduceDifferentHashes()
    {
        // PBKDF2 uses a random salt per call
        var h1 = _sut.Hash("MyPassword1!");
        var h2 = _sut.Hash("MyPassword1!");
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        var hash = _sut.Hash("MyPassword1!");
        Assert.True(_sut.Verify("MyPassword1!", hash));
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var hash = _sut.Hash("MyPassword1!");
        Assert.False(_sut.Verify("WrongPassword", hash));
    }

    [Fact]
    public void Verify_RoundTrip_MultiplePasswords()
    {
        foreach (var pw in new[] { "Short1!", "A longer passphrase 123", "P@$$w0rd" })
        {
            var hash = _sut.Hash(pw);
            Assert.True(_sut.Verify(pw, hash), $"Verify failed for: {pw}");
        }
    }
}
