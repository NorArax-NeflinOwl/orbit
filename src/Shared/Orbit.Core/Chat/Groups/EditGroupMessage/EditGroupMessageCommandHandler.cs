using Orbit.Core.Abstractions;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Chat.Groups.EditGroupMessage;

/// <summary>
/// Rewrites every copy of one group message. Only its sender may: unlike deleting, which a group admin
/// can also do (see ChatGroup.CanDeleteMessageFrom), putting different words in someone's mouth is not
/// something moderation should be able to do.
/// </summary>
public sealed class EditGroupMessageCommandHandler : IRequestHandler<EditGroupMessageCommand, bool>
{
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public EditGroupMessageCommandHandler(
        IChatMessageRepository chatMessageRepository, ILiveUpdatePublisher liveUpdatePublisher)
    {
        _chatMessageRepository = chatMessageRepository;
        _liveUpdatePublisher = liveUpdatePublisher;
    }

    public async Task<bool> HandleAsync(EditGroupMessageCommand request, CancellationToken cancellationToken)
    {
        var copies = await _chatMessageRepository.GetGroupMessageCopiesAsync(request.GroupMessageId, cancellationToken);
        if (copies.Count == 0 || copies.Any(copy => copy.SenderUserId != request.RequestingUserId))
        {
            return false;
        }

        var editedAtUtc = DateTimeOffset.UtcNow;
        foreach (var copy in copies)
        {
            // A copy the caller sent no replacement for is left as it was rather than being emptied -
            // a member who joined since is a recipient the sender's browser may not have keys for.
            var replacement = request.Copies.FirstOrDefault(candidate => candidate.RecipientUserId == copy.RecipientUserId);
            if (replacement is null)
            {
                continue;
            }

            await _chatMessageRepository.UpdateContentAsync(
                copy.Id, replacement.CiphertextBase64, replacement.NonceBase64, editedAtUtc, cancellationToken);
        }

        // Everybody holding a copy, which is the audience the posting itself reached. Taken from the
        // copies rather than from the group's membership: a copy is exactly who was written to, and a
        // member who joined afterwards has nothing here to be told about.
        await _liveUpdatePublisher.ChatChangedAsync(
            [.. copies.Select(copy => copy.RecipientUserId).Distinct()], cancellationToken);

        return true;
    }
}
