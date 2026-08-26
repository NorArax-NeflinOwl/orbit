using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Orbit.Mobile.Crypto;

/// <summary>
/// A P-256 key as JWK, which is the format the private-key backup is stored in - WebCrypto exports it
/// that way and .NET has no native EC JWK support, so the mapping to <see cref="ECParameters"/> is
/// written out here.
///
/// The coordinates are base64url without padding, and each is exactly 32 bytes for P-256 including any
/// leading zeros. Dropping a leading zero byte produces a key that is wrong roughly one time in 256 -
/// rare enough to pass a casual test and fail in the field, which is why <see cref="FromBase64Url"/>
/// pads rather than trusting the length it is given.
/// </summary>
internal sealed record JsonWebKey(
    [property: JsonPropertyName("kty")] string KeyType,
    [property: JsonPropertyName("crv")] string Curve,
    [property: JsonPropertyName("x")] string X,
    [property: JsonPropertyName("y")] string Y,
    [property: JsonPropertyName("d")] string? D,
    [property: JsonPropertyName("ext")] bool Extractable,
    [property: JsonPropertyName("key_ops")] IReadOnlyList<string> KeyOperations)
{
    private const int CoordinateSizeBytes = 32;

    /// <summary>What WebCrypto asks for when it imports this back: an extractable key that can agree keys.</summary>
    private static readonly IReadOnlyList<string> DeriveKeyOnly = ["deriveKey"];

    public static JsonWebKey FromPrivateKey(ECParameters parameters)
        => new(
            KeyType: "EC",
            Curve: "P-256",
            X: ToBase64Url(parameters.Q.X!),
            Y: ToBase64Url(parameters.Q.Y!),
            D: ToBase64Url(parameters.D!),
            Extractable: true,
            KeyOperations: DeriveKeyOnly);

    public ECParameters ToPrivateKeyParameters()
    {
        if (!string.Equals(Curve, "P-256", StringComparison.Ordinal))
        {
            throw new CryptographicException($"Orbit only uses P-256 keys; this backup says '{Curve}'.");
        }

        return new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = FromBase64Url(X), Y = FromBase64Url(Y) },
            D = D is null ? null : FromBase64Url(D)
        };
    }

    private static string ToBase64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Left-padded to the curve's coordinate size, so a value that happened to start with a zero byte survives.</summary>
    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '=');
        var decoded = Convert.FromBase64String(padded);
        if (decoded.Length >= CoordinateSizeBytes)
        {
            return decoded;
        }

        var fixedWidth = new byte[CoordinateSizeBytes];
        decoded.CopyTo(fixedWidth, CoordinateSizeBytes - decoded.Length);
        return fixedWidth;
    }
}
