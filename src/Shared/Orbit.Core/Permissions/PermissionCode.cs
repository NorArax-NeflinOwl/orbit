using System.Security.Cryptography;

namespace Orbit.Core.Permissions;

/// <summary>
/// The code that unlocks one permission, as it is stored. A code can be replaced - see
/// <see cref="PermissionCodeStore.RotateAsync"/> - and <c>CreatedAtUtc</c> then says when the code that
/// is standing now was made, so a rotation is visible in the row rather than silent.
/// </summary>
public sealed record PermissionCode(ApplicationPermission Permission, string Code, DateTimeOffset CreatedAtUtc)
{
    /// <summary>Long enough not to be guessable, short enough to read off a screen and type by hand.</summary>
    private const int Length = 12;

    /// <summary>Crockford base32 without I, L, O and U: no character pair a person could confuse while copying.</summary>
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static PermissionCode Mint(ApplicationPermission permission, DateTimeOffset nowUtc)
        => new(permission, RandomCode(), nowUtc);

    /// <summary>Codes are read off a screen and typed back in, so case and stray spacing are the typist's.</summary>
    public static string Normalize(string code) => code.Trim().Replace(" ", string.Empty).ToUpperInvariant();

    private static string RandomCode()
        => string.Concat(Enumerable.Range(0, Length).Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]));
}
