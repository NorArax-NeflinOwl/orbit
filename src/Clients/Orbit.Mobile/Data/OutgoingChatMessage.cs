namespace Orbit.Mobile.Data;

/// <summary>
/// A message typed with no connection, waiting to be sent.
///
/// <b>Stored as plaintext, deliberately, and this is the one place the app keeps any.</b> The rule comes
/// from info/orbit-maui-plan.md §5.5: a group message is one ciphertext per current member and the
/// server accepts exactly one per member, so a message encrypted when it was typed and sent an hour
/// later carries a stale membership list and is correctly rejected. Encryption therefore has to happen
/// at send time, which means the text has to survive until then. One-to-one messages do not need this,
/// but following the same rule from the start is what stops group chat needing the outbox rewritten.
///
/// The exposure is real and bounded: only messages not yet sent, and only until they are. It is also
/// the strongest argument for encrypting the local database (§5.1, still open).
/// </summary>
public sealed class OutgoingChatMessage
{
    public long Id { get; set; }

    /// <summary>
    /// Where this is going. Exactly one of the two is set: a message is addressed to a person or to a
    /// group, and the two travel to different endpoints with different fan-out.
    /// </summary>
    public Guid? RecipientUserId { get; set; }

    /// <inheritdoc cref="RecipientUserId"/>
    public Guid? GroupId { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset QueuedAtUtc { get; set; }

    /// <summary>How many times sending this has failed - see NoteSynchronizer for why that is bounded.</summary>
    public int FailedAttempts { get; set; }
}
