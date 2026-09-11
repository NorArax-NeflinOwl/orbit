using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.MarkConversationAsRead;

/// <param name="ReadUpToUtc">
/// The SentAtUtc of the newest message the reader has actually had in view. Only the other party's
/// messages sent at or before it are marked; later ones stay unread until they are seen too.
///
/// A timestamp rather than a message id, because "up to" is an order and SentAtUtc is the order the
/// conversation is kept in - the repository applies it as one comparison in the same statement that
/// marks, with no lookup to turn an id into a position first. It is exact because the value is the
/// server's own: the client sends back the SentAtUtc it was given for that message, untouched.
///
/// Null means "everything", which is what the route did before this existed. Installed phone builds
/// keep calling it without one until a rebuilt APK replaces them, and they must keep working.
/// </param>
public sealed record MarkConversationAsReadCommand(Guid ReaderUserId, Guid OtherUserId, DateTimeOffset? ReadUpToUtc = null)
    : IRequest<bool>;
