namespace Orbit.Core.Abstractions;

/// <summary>
/// Generates and checks the short numeric codes emailed for address verification and password resets.
/// Implemented in Orbit.Api (alongside IPasswordHasher's implementation) so the domain layer stays free
/// of any dependency on how the codes are actually generated and hashed.
/// </summary>
public interface IVerificationCodeGenerator
{
    /// <summary>A fresh code, in the form the user will retype.</summary>
    string Generate();

    string Hash(string code);

    bool Verify(string code, string codeHash);
}
