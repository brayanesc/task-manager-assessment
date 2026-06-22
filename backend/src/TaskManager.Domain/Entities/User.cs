using TaskManager.Domain.Exceptions;

namespace TaskManager.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;

    private User() { }

    public static User Create(string email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email must not be empty.");

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash
        };
    }

    // Used by Infrastructure repositories to map rows without re-hashing or re-validating.
    public static User Reconstitute(Guid id, string email, string passwordHash) =>
        new() { Id = id, Email = email, PasswordHash = passwordHash };
}
