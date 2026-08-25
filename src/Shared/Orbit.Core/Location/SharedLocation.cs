using Orbit.Core.Abstractions;

namespace Orbit.Core.Location;

/// <summary>
/// One person's position, shared with one other person and encrypted for them alone. The ciphertext is
/// sealed in the sharer's browser under the pairwise key the two already use for chat, so Orbit's
/// servers relay a point they cannot read - the same guarantee a chat message has.
///
/// There is exactly one row per (sharer, recipient) pair and it is overwritten in place: the server and
/// the database keep no trail of where anyone has been, only where they are now. A client is free to
/// keep its own history locally; nothing here does.
///
/// <see cref="IsContinuous"/> separates "here is where I am" from "follow me for a while". Both store
/// the same single point; the flag tells the recipient whether to expect it to keep changing, and tells
/// the sharer's own client to keep refreshing it. Sharing ends by deleting the row (see
/// StopSharingLocationCommand), which is what makes stopping mean the position is gone rather than
/// merely stale.
/// </summary>
public sealed class SharedLocation
{
    public Guid Id { get; private set; }
    public Guid SharerUserId { get; private set; }
    public Guid RecipientUserId { get; private set; }

    /// <summary>Base64 AES-GCM ciphertext of the position, readable only by the two people whose keys made it.</summary>
    public string CiphertextBase64 { get; private set; }

    public string NonceBase64 { get; private set; }

    /// <summary>Whether the sharer is keeping this up to date, as opposed to having sent one fixed point.</summary>
    public bool IsContinuous { get; private set; }

    /// <summary>When the point currently stored was recorded. Replaced with the point, never accumulated.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private SharedLocation(
        Guid id, Guid sharerUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64,
        bool isContinuous, DateTimeOffset updatedAtUtc)
    {
        Id = id;
        SharerUserId = sharerUserId;
        RecipientUserId = recipientUserId;
        CiphertextBase64 = ciphertextBase64;
        NonceBase64 = nonceBase64;
        IsContinuous = isContinuous;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static SharedLocation Create(
        Guid sharerUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64, bool isContinuous)
    {
        if (sharerUserId == recipientUserId)
        {
            throw new InvalidRequestException("You can't share your location with yourself.");
        }

        return new SharedLocation(
            Guid.NewGuid(), sharerUserId, recipientUserId, ciphertextBase64, nonceBase64, isContinuous, DateTimeOffset.UtcNow);
    }

    /// <summary>Rebuilds a shared location from already-persisted values, bypassing creation rules.</summary>
    public static SharedLocation FromPersistence(
        Guid id, Guid sharerUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64,
        bool isContinuous, DateTimeOffset updatedAtUtc)
        => new(id, sharerUserId, recipientUserId, ciphertextBase64, nonceBase64, isContinuous, updatedAtUtc);

    /// <summary>
    /// Replaces the stored point with a newer one. The old ciphertext is overwritten rather than kept
    /// beside the new one - that is the whole of "no history" as far as this row is concerned.
    /// </summary>
    public void Refresh(string ciphertextBase64, string nonceBase64, bool isContinuous)
    {
        CiphertextBase64 = ciphertextBase64;
        NonceBase64 = nonceBase64;
        IsContinuous = isContinuous;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
