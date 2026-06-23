using TaskManager.Application.Common;
using TaskManager.Application.DTOs;
using TaskManager.Application.Interfaces;

namespace TaskManager.Application.UseCases;

public sealed class LoginUseCase(
    IUnitOfWork uow,
    IPasswordHasher hasher,
    ITokenService tokenService)
{
    public async Task<Result<AuthResponse>> ExecuteAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Result<AuthResponse>.Fail("Email and password are required.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await uow.Users.GetByEmailAsync(normalizedEmail, ct);

        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
            return Result<AuthResponse>.Unauthorized("Invalid email or password.");

        var token = tokenService.GenerateToken(user);
        return Result<AuthResponse>.Ok(new AuthResponse(token, user.Email));
    }
}
