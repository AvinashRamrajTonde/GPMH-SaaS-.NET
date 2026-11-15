using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace GPMH.Infrastructure.Identity;

public sealed class PasswordHasherService : IPasswordHasherService
{
    private static readonly User DummyUser = new()
    {
        Username = "_hasher",
        FullNameEn = "Hasher",
        FullNameMr = "Hasher",
        PasswordHash = string.Empty,
    };

    private readonly PasswordHasher<User> _inner = new();

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return _inner.HashPassword(DummyUser, password);
    }

    public bool VerifyPassword(string passwordHash, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrWhiteSpace(providedPassword))
        {
            return false;
        }

        PasswordVerificationResult result = _inner.VerifyHashedPassword(DummyUser, passwordHash, providedPassword);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}

