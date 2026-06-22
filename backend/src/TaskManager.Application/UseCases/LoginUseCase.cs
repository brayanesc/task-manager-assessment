using TaskManager.Application.DTOs;
using TaskManager.Application.Interfaces;

namespace TaskManager.Application.UseCases;

public sealed class LoginUseCase(
    IUserRepository userRepo,
    IPasswordHasher hasher,
    ITokenService tokenService)
{
    public async Task<AuthResponse?> ExecuteAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return null;

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userRepo.GetByEmailAsync(normalizedEmail, ct);

        if (user is null) return null;
        if (!hasher.Verify(request.Password, user.PasswordHash)) return null;

        var token = tokenService.GenerateToken(user);
        return new AuthResponse(token, user.Email);
    }
}
