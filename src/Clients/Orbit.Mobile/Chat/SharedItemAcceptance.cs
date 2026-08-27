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

    /// <summary>
    /// Whether the offer has already been taken up. False when the server cannot be reached or has
    /// never heard of the share: an offer that might still be open is worth showing, and one shown in
    /// error costs a tap and an honest answer.
    /// </summary>
    public async Task<bool> WasAcceptedAsync(
        SharedItemInvitation invitation, CancellationToken cancellationToken = default)
    {
        try
        {
            var accepted = invitation.Kind switch
            {
                SharedItemKind.Note => await _notes.IsShareAcceptedAsync(invitation.ShareId, cancellationToken),
                SharedItemKind.TaskList => await _tasks.IsShareAcceptedAsync(invitation.ShareId, cancellationToken),
                SharedItemKind.CalendarEvent => await _calendar.IsShareAcceptedAsync(invitation.ShareId, cancellationToken),
                _ => await _inventory.IsShareAcceptedAsync(invitation.ShareId, cancellationToken)
            };

            return accepted is true;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return false;
        }
    }
}
