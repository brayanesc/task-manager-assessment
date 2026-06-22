using TaskManager.Application.DTOs;
using TaskManager.Application.Interfaces;
using TaskManager.Domain.Entities;
using TaskManager.Domain.Exceptions;

namespace TaskManager.Application.UseCases;

public sealed class RegisterUserUseCase(IUserRepository userRepo, IPasswordHasher hasher)
{
    public async Task ExecuteAsync(RegisterRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await userRepo.ExistsAsync(normalizedEmail, ct))
            throw new DomainException("Email is already registered.");

        var hash = hasher.Hash(request.Password);
        var user = User.Create(request.Email, hash); // User.Create trims and lowercases
        await userRepo.CreateAsync(user, ct);
    }

    private static void ValidateRequest(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new DomainException("Email must not be empty.");

        if (!IsValidEmailFormat(request.Email.Trim()))
            throw new DomainException("Email format is invalid.");

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new DomainException("Password must not be empty.");

        if (request.Password.Trim().Length > 0 && request.Password.Length < 8)
            throw new DomainException("Password must be at least 8 characters.");
    }

    private static bool IsValidEmailFormat(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0) return false;
        var domain = email[(atIndex + 1)..];
        return domain.Contains('.') && domain.Length > 2;
    }
}
