using Barlas.Application.Abstractions;
using Barlas.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Barlas.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>هش رمز با PBKDF2 از ASP.NET Core Identity (بدون جدول‌های Identity).</summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(User user, string password) => _inner.HashPassword(user, password);

    public bool Verify(User user, string password) =>
        !string.IsNullOrEmpty(user.PasswordHash) &&
        _inner.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
