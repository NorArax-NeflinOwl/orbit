using Microsoft.AspNetCore.Identity;
using Orbit.Core.Abstractions;

namespace Orbit.Api.Auth;

/// <summary>
/// Wraps ASP.NET Core's own <see cref="PasswordHasher{TUser}"/> (PBKDF2 under the hood) behind
/// <see cref="IPasswordHasher"/>, so Orbit.Core never references ASP.NET Core Identity directly.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    // PasswordHasher<TUser> never actually reads the instance it's given - the type parameter is a
    // leftover from ASP.NET Core Identity's design - so a throwaway object is a safe stand-in here.
    private readonly PasswordHasher<object> _innerHasher = new();

    public string Hash(string password) => _innerHasher.HashPassword(new object(), password);

    public bool Verify(string password, string passwordHash)
        => _innerHasher.VerifyHashedPassword(new object(), passwordHash, password) != PasswordVerificationResult.Failed;
}
