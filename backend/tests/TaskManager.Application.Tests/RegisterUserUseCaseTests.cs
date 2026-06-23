using Moq;
using TaskManager.Application.Common;
using TaskManager.Application.DTOs;
using TaskManager.Application.Interfaces;
using TaskManager.Application.UseCases;
using TaskManager.Domain.Entities;

namespace TaskManager.Application.Tests;

public class RegisterUserUseCaseTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly RegisterUserUseCase _sut;

    public RegisterUserUseCaseTests()
    {
        _uow.Setup(u => u.Users).Returns(_userRepo.Object);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _userRepo.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed_password");
        _sut = new RegisterUserUseCase(_uow.Object, _hasher.Object);
    }

    // ── Happy path (AC: account created, password stored hashed) ─────────────

    [Fact]
    public async Task ExecuteAsync_WithValidInput_ReturnsOkAndCreatesUser()
    {
        var result = await _sut.ExecuteAsync(new RegisterRequest("alice@example.com", "P@ssword1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepo.Verify(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
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

    // ── Duplicate email (AC: unique email required) ───────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithDuplicateEmail_ReturnsConflict()
    {
        _userRepo.Setup(r => r.ExistsAsync("alice@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.ExecuteAsync(new RegisterRequest("alice@example.com", "P@ssword1"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultKind.Conflict, result.Kind);
        Assert.Contains("email", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_OnDuplicateEmail_NeverCreatesUserOrCommits()
    {
        _userRepo.Setup(r => r.ExistsAsync("alice@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _sut.ExecuteAsync(new RegisterRequest("alice@example.com", "P@ssword1"), CancellationToken.None);

        _userRepo.Verify(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Email validation ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithEmptyEmail_ReturnsValidationFail(string email)
    {
        var result = await _sut.ExecuteAsync(new RegisterRequest(email, "P@ssword1"), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ResultKind.Validation, result.Kind);
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@dot")]
    [InlineData("@nodomain.com")]
    public async Task ExecuteAsync_WithInvalidEmailFormat_ReturnsValidationFail(string email)
    {
        var result = await _sut.ExecuteAsync(new RegisterRequest(email, "P@ssword1"), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ResultKind.Validation, result.Kind);
        Assert.Contains("email", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Password validation ───────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithEmptyPassword_ReturnsValidationFail(string password)
    {
        var result = await _sut.ExecuteAsync(new RegisterRequest("alice@example.com", password), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ResultKind.Validation, result.Kind);
    }

    [Fact]
    public async Task ExecuteAsync_WithPasswordShorterThan8Chars_ReturnsValidationFail()
    {
        var result = await _sut.ExecuteAsync(new RegisterRequest("alice@example.com", "abc1234"), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Contains("8", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithPasswordExactly8Chars_ReturnsOk()
    {
        var result = await _sut.ExecuteAsync(new RegisterRequest("alice@example.com", "abcd1234"), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }
}
