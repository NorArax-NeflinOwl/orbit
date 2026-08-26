using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.EditGroupMessage;

/// <summary>
/// Rewrites every copy of one group message. Only its sender may: unlike deleting, which a group admin
/// can also do (see ChatGroup.CanDeleteMessageFrom), putting different words in someone's mouth is not
/// something moderation should be able to do.
/// </summary>
public sealed class EditGroupMessageCommandHandler : IRequestHandler<EditGroupMessageCommand, bool>
{
    private readonly IChatMessageRepository _chatMessageRepository;

    public EditGroupMessageCommandHandler(IChatMessageRepository chatMessageRepository)
    {
        _chatMessageRepository = chatMessageRepository;
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

        return true;
    }
}
