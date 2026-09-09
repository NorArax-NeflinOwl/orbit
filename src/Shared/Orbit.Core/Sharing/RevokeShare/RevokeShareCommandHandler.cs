using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Chat;
using Orbit.Core.Inventories;
using Orbit.Core.LiveUpdates;
using Orbit.Core.Notes;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;

namespace Orbit.Core.Sharing.RevokeShare;

/// <summary>
/// Withdraws one share, whichever kind it is about - the four repositories meet here for the reason
/// GetSharesWithQueryHandler gives about its own four.
///
/// Every removal is scoped to the owner, so this can only take back access the caller themselves gave.
/// A share belonging to somebody else, or one already gone, answers false rather than throwing: the
/// page says the same thing for both, and telling them apart would say whether a share id exists.
///
/// The invitation goes with it. What the recipient pressed "Accept" on is a chat message, and dropping
/// the grant while leaving that message behind leaves an offer in the conversation that now leads
/// nowhere - so the message is taken back too, the same way the sender taking their own message back
/// would: the words go, the line stays, and it says who removed it (see ChatMessage.Delete).
/// </summary>
public sealed class RevokeShareCommandHandler : IRequestHandler<RevokeShareCommand, bool>
{
    private readonly INoteShareRepository _noteShareRepository;
    private readonly ITaskListShareRepository _taskListShareRepository;
    private readonly ICalendarEventShareRepository _calendarEventShareRepository;
    private readonly IInventoryShareRepository _inventoryShareRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public RevokeShareCommandHandler(
        INoteShareRepository noteShareRepository,
        ITaskListShareRepository taskListShareRepository,
        ICalendarEventShareRepository calendarEventShareRepository,
        IInventoryShareRepository inventoryShareRepository,
        IChatMessageRepository chatMessageRepository,
        ILiveUpdatePublisher liveUpdatePublisher)
    {
        _noteShareRepository = noteShareRepository;
        _taskListShareRepository = taskListShareRepository;
        _calendarEventShareRepository = calendarEventShareRepository;
        _inventoryShareRepository = inventoryShareRepository;
        _chatMessageRepository = chatMessageRepository;
        _liveUpdatePublisher = liveUpdatePublisher;
    }

    public async Task<bool> HandleAsync(RevokeShareCommand request, CancellationToken cancellationToken)
    {
        if (!await RemoveTheGrantAsync(request, cancellationToken))
        {
            return false;
        }

        await TakeTheInvitationBackAsync(request, cancellationToken);
        return true;
    }

    private Task<bool> RemoveTheGrantAsync(RevokeShareCommand request, CancellationToken cancellationToken) => request.Kind switch
    {
        SharedItemKind.Note => _noteShareRepository.RemoveAsync(request.OwnerUserId, request.ShareId, cancellationToken),
        SharedItemKind.TaskList => _taskListShareRepository.RemoveAsync(request.OwnerUserId, request.ShareId, cancellationToken),
        SharedItemKind.CalendarEvent => _calendarEventShareRepository.RemoveAsync(request.OwnerUserId, request.ShareId, cancellationToken),
        SharedItemKind.Inventory => _inventoryShareRepository.RemoveAsync(request.OwnerUserId, request.ShareId, cancellationToken),
        // A position is not a share row at all - it lives in OP_LOCATIONS_SHARED and is withdrawn from
        // the map, where it was offered. Named rather than swept into a default, so a fifth kind that
        // does have a row fails to compile here instead of quietly answering "nothing to do".
        SharedItemKind.Location => Task.FromResult(false),
        _ => Task.FromResult(false)
    };

    /// <summary>
    /// Usually one message; more when the same share was offered again as a reminder, which reuses the
    /// share rather than making a second one - so every message that named it goes.
    ///
    /// Nothing here depends on the message being found. A share made before invitations recorded which
    /// share they announced has nothing to match, and one offered from a client that does not say so has
    /// nothing either; in both cases the access is still withdrawn, which is what was asked for.
    /// </summary>
    private async Task TakeTheInvitationBackAsync(RevokeShareCommand request, CancellationToken cancellationToken)
    {
        var everybodyInvolved = await _chatMessageRepository.MarkShareAnnouncementsDeletedAsync(
            request.ShareId, request.OwnerUserId, DateTimeOffset.UtcNow, cancellationToken);
        if (everybodyInvolved.Count == 0)
        {
            return;
        }

        // The recipient first of all: without this the invitation stays on their screen, still offering
        // an "Accept", until the slow poll comes round - see DeleteChatMessageCommandHandler.
        await _liveUpdatePublisher.ChatChangedAsync(everybodyInvolved, cancellationToken);
    }
}
