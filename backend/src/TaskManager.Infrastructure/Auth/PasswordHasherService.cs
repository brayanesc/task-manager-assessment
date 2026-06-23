using Microsoft.AspNetCore.Identity;
using TaskManager.Application.Interfaces;

namespace TaskManager.Infrastructure.Auth;

public sealed class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<string> _hasher = new();

    public string Hash(string plaintext) =>
        _hasher.HashPassword(string.Empty, plaintext);

    public bool Verify(string plaintext, string hash)
    {
        var result = _hasher.VerifyHashedPassword(string.Empty, hash, plaintext);
        return result != PasswordVerificationResult.Failed;
    }
}
