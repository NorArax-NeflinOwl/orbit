using System.Security.Cryptography;
using System.Text;

namespace Orbit.Core.Permissions;

/// <summary>
/// Turns one deployment secret into one code per permission, and back again. Deriving the codes rather
/// than storing them means the server holds a single secret instead of a table of valid codes, and that
/// rotating the secret invalidates every code at once.
///
/// The secret never leaves the server. The Blazor client is downloaded to the browser, so a code shipped
/// with it would be readable by anyone who has the app - the client only ever sends what somebody typed
/// and is told whether it matched.
/// </summary>
public sealed class PermissionCodeAuthority
{
    /// <summary>Long enough not to be guessable, short enough to read off a screen and type by hand.</summary>
    private const int CodeLength = 12;

    /// <summary>Crockford base32 without I, L, O and U: no character pair that a person could confuse while copying a code.</summary>
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private readonly byte[] _secret;

    public PermissionCodeAuthority(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        _secret = Encoding.UTF8.GetBytes(secret);
    }

    public string CodeFor(ApplicationPermission permission)
    {
        var digest = HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(permission.ToString()));
        var code = new StringBuilder(CodeLength);
        for (var index = 0; index < CodeLength; index++)
        {
            code.Append(Alphabet[digest[index] % Alphabet.Length]);
        }

        return code.ToString();
    }

    /// <summary>
    /// The permission the given code unlocks, or null if it unlocks nothing. Every candidate is compared
    /// even after one matches: stopping early would let the time taken reveal which permission a typed
    /// code was closest to.
    /// </summary>
    public ApplicationPermission? Match(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var typed = Encoding.UTF8.GetBytes(Normalize(code));
        ApplicationPermission? matched = null;
        foreach (var permission in Enum.GetValues<ApplicationPermission>())
        {
            if (CryptographicOperations.FixedTimeEquals(typed, Encoding.UTF8.GetBytes(CodeFor(permission))))
            {
                matched = permission;
            }
        }

        return matched;
    }

    /// <summary>Codes are read off a screen and typed back in, so case and stray spacing are the typist's, not a different code.</summary>
    private static string Normalize(string code) => code.Trim().Replace(" ", string.Empty).ToUpperInvariant();
}
