using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.MarkGroupConversationAsRead;

/// <summary>Marks everything addressed to this reader in the group as read - the group counterpart of MarkConversationAsReadCommand.</summary>
/// <param name="ReadUpToUtc">
/// The SentAtUtc of the newest message the reader has actually had in view; only copies sent at or
/// before it are marked, and null marks all of them - see MarkConversationAsReadCommand. A timestamp
/// suits a group even better than a one-to-one conversation: every copy of one group message carries
/// the same SentAtUtc (see ChatMessage.CreateForGroup), so the cut never falls between two copies of
/// the same message, whichever copy's id the reader's screen happened to be holding.
/// </param>
public sealed record MarkGroupConversationAsReadCommand(Guid ReaderUserId, Guid GroupId, DateTimeOffset? ReadUpToUtc = null)
    : IRequest<bool>;
