using System.Security.Cryptography;
using Orbit.Core.Abstractions;

namespace Orbit.Core.Sharing;

/// <summary>
/// A link that shows one item to anybody who has the URL, without an account. Unlike a NoteShare and
/// its siblings, this grants no access to a *person* - there is nobody named on it - so the only thing
/// standing between the item and the internet is that the token is unguessable, and the only thing it
/// ever permits is reading. Someone who signs in can claim a link, which creates an ordinary read-only
/// share of it in their own account and leaves this link untouched.
///
/// Revoked rather than deleted, so a link that stops working reads as "the owner turned this off"
/// rather than "this never existed", and so an owner can see what they have handed out.
/// </summary>
public sealed class PublicShareLink
{
    /// <summary>256 bits of randomness, url-safe. Long enough that guessing one is not a strategy, and short enough to paste into a chat.</summary>
    private const int TokenBytes = 32;

    public Guid Id { get; private set; }

    /// <summary>The secret in the URL. This is the whole of the access check, which is why it is generated with a cryptographic RNG rather than a Guid.</summary>
    public string Token { get; private set; }

    public Guid OwnerUserId { get; private set; }
    public SharedItemType ItemType { get; private set; }
    public Guid ItemId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public bool IsRevoked => RevokedAtUtc is not null;

    private PublicShareLink(
        Guid id, string token, Guid ownerUserId, SharedItemType itemType, Guid itemId,
        DateTimeOffset createdAtUtc, DateTimeOffset? revokedAtUtc)
    {
        Id = id;
        Token = token;
        OwnerUserId = ownerUserId;
        ItemType = itemType;
        ItemId = itemId;
        CreatedAtUtc = createdAtUtc;
        RevokedAtUtc = revokedAtUtc;
    }

    public static PublicShareLink Create(Guid ownerUserId, SharedItemType itemType, Guid itemId)
        => new(Guid.NewGuid(), GenerateToken(), ownerUserId, itemType, itemId, DateTimeOffset.UtcNow, revokedAtUtc: null);

    public static PublicShareLink FromPersistence(
        Guid id, string token, Guid ownerUserId, SharedItemType itemType, Guid itemId,
        DateTimeOffset createdAtUtc, DateTimeOffset? revokedAtUtc)
        => new(id, token, ownerUserId, itemType, itemId, createdAtUtc, revokedAtUtc);

    /// <summary>Idempotent - revoking an already-revoked link is a no-op rather than a new timestamp.</summary>
    public void Revoke() => RevokedAtUtc ??= DateTimeOffset.UtcNow;

    /// <summary>
    /// Refuses a private item outright. Its title and content are sealed with a key only its owner's
    /// browser holds (see PrivateContentSealer), so a public reader would be handed ciphertext - and
    /// offering to publish something the owner marked private is the wrong thing to offer at all.
    /// </summary>
    public static void EnsureShareable(bool isPrivate)
    {
        if (isPrivate)
        {
            throw new InvalidRequestException("A private item can't be shared with a link.");
        }
    }

    private static string GenerateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
