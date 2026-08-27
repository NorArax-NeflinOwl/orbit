using Orbit.Mobile.Api;

namespace Orbit.Mobile.Chat;

/// <summary>
/// Takes somebody up on an offer to share something. One place rather than a branch in each screen that
/// can show an offer: which endpoint accepts it follows from the kind of thing offered, and that is one
/// fact, not four.
///
/// Accepting does not fetch the item. It creates the copy on the server; the feature's own synchroniser
/// brings it down on its next run, which is also how a share accepted in the browser reaches the phone.
/// </summary>
public sealed class SharedItemAcceptance
{
    private readonly NotesClient _notes;
    private readonly TasksClient _tasks;
    private readonly CalendarClient _calendar;
    private readonly InventoryClient _inventory;

    public SharedItemAcceptance(
        NotesClient notes, TasksClient tasks, CalendarClient calendar, InventoryClient inventory)
    {
        _notes = notes;
        _tasks = tasks;
        _calendar = calendar;
        _inventory = inventory;
    }

    public Task<bool> AcceptAsync(SharedItemInvitation invitation, CancellationToken cancellationToken = default)
        => invitation.Kind switch
        {
            SharedItemKind.Note => _notes.AcceptShareAsync(invitation.ShareId, cancellationToken),
            SharedItemKind.TaskList => _tasks.AcceptShareAsync(invitation.ShareId, cancellationToken),
            SharedItemKind.CalendarEvent => _calendar.AcceptShareAsync(invitation.ShareId, cancellationToken),
            _ => _inventory.AcceptShareAsync(invitation.ShareId, cancellationToken)
        };
}
