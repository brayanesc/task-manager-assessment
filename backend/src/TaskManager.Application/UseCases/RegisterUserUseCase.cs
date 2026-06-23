using TaskManager.Application.Common;
using TaskManager.Application.DTOs;
using TaskManager.Application.Interfaces;
using TaskManager.Domain.Entities;

namespace TaskManager.Application.UseCases;

public sealed class RegisterUserUseCase(IUnitOfWork uow, IPasswordHasher hasher)
{
    public async Task<Result<Unit>> ExecuteAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var validationError = Validate(request);
        if (validationError is not null)
            return Result<Unit>.Fail(validationError);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await uow.Users.ExistsAsync(normalizedEmail, ct))
            return Result<Unit>.Conflict("Email is already registered.");

        var hash = hasher.Hash(request.Password);
        var user = User.Create(request.Email, hash);
        await uow.Users.CreateAsync(user, ct);
        await uow.CommitAsync(ct);
        return Result<Unit>.Ok(Unit.Value);
    }

    private static string? Validate(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return "Email must not be empty.";

        if (!IsValidEmailFormat(request.Email.Trim()))
            return "Email format is invalid.";

        if (string.IsNullOrWhiteSpace(request.Password))
            return "Password must not be empty.";

        if (request.Password.Length < 8)
            return "Password must be at least 8 characters.";

        return null;
    }

    private static bool IsValidEmailFormat(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0) return false;
        var domain = email[(atIndex + 1)..];
        return domain.Contains('.') && domain.Length > 2;
    }
}
