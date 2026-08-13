namespace Orbit.Core.Abstractions;

/// <summary>
/// Hashes and verifies passwords. Implemented in Orbit.Api on top of ASP.NET Core's own hasher, so the
/// domain layer stays free of any dependency on how passwords are actually secured.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
