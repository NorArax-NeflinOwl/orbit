using System.ComponentModel.DataAnnotations.Schema;

namespace Orbit.Mobile.Data;

/// <summary>
/// A group as the phone holds it, so the group list and a group conversation both open with no
/// connection - the same reason <see cref="LocalContact"/> exists for one-to-one chat.
/// </summary>
public sealed class LocalChatGroup
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>The signed-in user's own role, "Admin" or "Member" - what the screen may offer.</summary>
    public string OwnRole { get; set; } = string.Empty;

    /// <inheritdoc cref="LocalContact.IsArchived"/>
    public bool IsArchived { get; set; }

    /// <inheritdoc cref="LocalContact.IsPinned"/>
    [NotMapped]
    public bool IsPinned { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Everyone in the group, the signed-in user included. Stored as one JSON column rather than a child
    /// table because membership is only ever read and written whole, with the group.
    /// </summary>
    public IReadOnlyList<LocalChatGroupMember> Members { get; set; } = [];
}

/// <summary>
/// One member, with the two things the phone needs about them that the group endpoint does not carry:
/// a name to show, and the key their copy of a message is sealed with.
///
/// <b>The cached key is for reading, not for sending.</b> Opening a group message needs whichever key
/// the copy was sealed against, and that has to work offline. Sending fetches keys fresh, for the reason
/// <see cref="LocalContact"/> gives: a key the member has since replaced would seal a message nobody
/// can open.
/// </summary>
public sealed record LocalChatGroupMember(Guid UserId, string Role, string DisplayName, string? PublicKeyBase64);
