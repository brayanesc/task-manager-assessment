using Moq;
using TaskManager.Application.DTOs;
using TaskManager.Application.Interfaces;
using TaskManager.Application.UseCases;
using TaskManager.Domain.Entities;
using TaskManager.Domain.Exceptions;

namespace TaskManager.Application.Tests;

public class RegisterUserUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly RegisterUserUseCase _sut;

    public RegisterUserUseCaseTests()
    {
        _userRepo
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepo
            .Setup(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed_password");
        _sut = new RegisterUserUseCase(_userRepo.Object, _hasher.Object);
    }

    // ── Happy path (AC: account created, password stored hashed) ─────────────

    [Fact]
    public async Task ExecuteAsync_WithValidInput_CreatesUser()
    {
        await _sut.ExecuteAsync(new RegisterRequest("alice@example.com", "P@ssword1"), CancellationToken.None);

        _userRepo.Verify(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HashesPasswordBeforeStoring()
    {
        _hasher.Setup(h => h.Hash("P@ssword1")).Returns("stored_hash");

        await _sut.ExecuteAsync(new RegisterRequest("alice@example.com", "P@ssword1"), CancellationToken.None);

        _hasher.Verify(h => h.Hash("P@ssword1"), Times.Once);
        _userRepo.Verify(r => r.CreateAsync(
            It.Is<User>(u => u.PasswordHash == "stored_hash"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NormalizesEmailToLowercaseForStorage()
    {
        await _sut.ExecuteAsync(new RegisterRequest("ALICE@EXAMPLE.COM", "P@ssword1"), CancellationToken.None);

        _userRepo.Verify(r => r.CreateAsync(
            It.Is<User>(u => u.Email == "alice@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Duplicate email (AC: must be unique) ──────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithDuplicateEmail_ThrowsDomainException()
    {
        _userRepo.Setup(r => r.ExistsAsync("alice@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ExecuteAsync(new RegisterRequest("alice@example.com", "P@ssword1"), CancellationToken.None));

        Assert.Contains("email", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_OnDuplicateEmail_NeverCallsCreate()
    {
        _userRepo.Setup(r => r.ExistsAsync("alice@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ExecuteAsync(new RegisterRequest("alice@example.com", "P@ssword1"), CancellationToken.None));

        _userRepo.Verify(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Email validation ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithEmptyEmail_ThrowsDomainException(string email)
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ExecuteAsync(new RegisterRequest(email, "P@ssword1"), CancellationToken.None));
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@dot")]
    [InlineData("@nodomain.com")]
    public async Task ExecuteAsync_WithInvalidEmailFormat_ThrowsDomainException(string email)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ExecuteAsync(new RegisterRequest(email, "P@ssword1"), CancellationToken.None));
        Assert.Contains("email", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Password validation ───────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithEmptyPassword_ThrowsDomainException(string password)
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ExecuteAsync(new RegisterRequest("alice@example.com", password), CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithPasswordShorterThan8Chars_ThrowsDomainException()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _sut.ExecuteAsync(new RegisterRequest("alice@example.com", "abc1234"), CancellationToken.None));
        Assert.Contains("8", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithPasswordExactly8Chars_Succeeds()
    {
        await _sut.ExecuteAsync(new RegisterRequest("alice@example.com", "abcd1234"), CancellationToken.None);
        _userRepo.Verify(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
