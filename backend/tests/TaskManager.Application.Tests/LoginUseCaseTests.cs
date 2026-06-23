using Moq;
using TaskManager.Application.Common;
using TaskManager.Application.DTOs;
using TaskManager.Application.Interfaces;
using TaskManager.Application.UseCases;
using TaskManager.Domain.Entities;

namespace TaskManager.Application.Tests;

public class LoginUseCaseTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly LoginUseCase _sut;

    private static readonly User Alice =
        User.Reconstitute(Guid.NewGuid(), "alice@example.com", "stored_hash");

    public LoginUseCaseTests()
    {
        _uow.Setup(u => u.Users).Returns(_userRepo.Object);
        _sut = new LoginUseCase(_uow.Object, _hasher.Object, _tokenService.Object);
    }

    // ── Happy path (AC: receive JWT on valid credentials) ────────────────────

    [Fact]
    public async Task ExecuteAsync_WithValidCredentials_ReturnsOkWithToken()
    {
        _userRepo.Setup(r => r.GetByEmailAsync("alice@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(Alice);
        _hasher.Setup(h => h.Verify("P@ssword1", "stored_hash")).Returns(true);
        _tokenService.Setup(t => t.GenerateToken(Alice)).Returns("jwt.token.value");

        var result = await _sut.ExecuteAsync(new LoginRequest("alice@example.com", "P@ssword1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("jwt.token.value", result.Value!.Token);
        Assert.Equal("alice@example.com", result.Value.Email);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCredentials_CallsGenerateToken()
    {
        _userRepo.Setup(r => r.GetByEmailAsync("alice@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(Alice);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _tokenService.Setup(t => t.GenerateToken(It.IsAny<User>())).Returns("token");

        await _sut.ExecuteAsync(new LoginRequest("alice@example.com", "P@ssword1"), CancellationToken.None);

        _tokenService.Verify(t => t.GenerateToken(Alice), Times.Once);
    }

    // ── AC: 401 Unauthorized when credentials invalid ─────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithNonExistentEmail_ReturnsUnauthorized()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _sut.ExecuteAsync(new LoginRequest("ghost@example.com", "P@ssword1"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultKind.Unauthorized, result.Kind);
    }

    [Fact]
    public async Task ExecuteAsync_WithWrongPassword_ReturnsUnauthorized()
    {
        _userRepo.Setup(r => r.GetByEmailAsync("alice@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(Alice);
        _hasher.Setup(h => h.Verify("WrongPassword", "stored_hash")).Returns(false);

        var result = await _sut.ExecuteAsync(new LoginRequest("alice@example.com", "WrongPassword"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultKind.Unauthorized, result.Kind);
    }

    [Fact]
    public async Task ExecuteAsync_WithWrongPassword_NeverCallsGenerateToken()
    {
        _userRepo.Setup(r => r.GetByEmailAsync("alice@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(Alice);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        await _sut.ExecuteAsync(new LoginRequest("alice@example.com", "bad"), CancellationToken.None);

        _tokenService.Verify(t => t.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    // ── Empty / null inputs ───────────────────────────────────────────────────

    [Theory]
    [InlineData("", "P@ssword1")]
    [InlineData("   ", "P@ssword1")]
    [InlineData("alice@example.com", "")]
    [InlineData("alice@example.com", "   ")]
    public async Task ExecuteAsync_WithEmptyCredentials_ReturnsValidationFail(string email, string password)
    {
        var result = await _sut.ExecuteAsync(new LoginRequest(email, password), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ResultKind.Validation, result.Kind);
    }

    // ── Email look-up is case-insensitive ─────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_NormalizesEmailForLookup()
    {
        _userRepo.Setup(r => r.GetByEmailAsync("alice@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(Alice);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _tokenService.Setup(t => t.GenerateToken(It.IsAny<User>())).Returns("token");

        await _sut.ExecuteAsync(new LoginRequest("ALICE@EXAMPLE.COM", "P@ssword1"), CancellationToken.None);

        _userRepo.Verify(r => r.GetByEmailAsync("alice@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }
}
