using Orbit.Contracts.Notes;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Notes;

/// <summary>
/// One row of the notes list. A view's worth of a <see cref="LocalNote"/> - what to show, and the two
/// things the user has to be told about it: whether the app is still holding a change, and whether it
/// can be changed at all right now.
/// </summary>
/// <param name="IsHidden">
/// A private note while private things are locked. The row still appears - a note vanishing from the
/// list would look like it had been deleted - but says nothing about itself until it is unlocked.
/// </param>
public sealed record NoteListItem(
    Guid LocalId, string Title, DateTimeOffset UpdatedAtUtc, bool HasUnsentChanges, OfflineEditRefusal Refusal,
    bool IsHidden = false)
{
    public static NoteListItem From(
        LocalNote note, bool hasUnsentChanges, INetworkStatus networkStatus, bool privateItemsAreUnlocked)
        => new(note.LocalId, note.Title, note.UpdatedAtUtc, hasUnsentChanges,
            OfflineEditPolicy.Evaluate(note, networkStatus),
            IsHidden: note.IsPrivate && !privateItemsAreUnlocked);

    /// <summary>What the row shows instead of the title while it is hidden.</summary>
    public string DisplayTitle => IsHidden ? "Private" : Title;

    /// <summary>Only a hidden row offers to unlock; every other row opens.</summary>
    public bool CanBeOpened => !IsHidden;

    public bool IsEditable => Refusal is OfflineEditRefusal.None;

    /// <summary>Empty when there is nothing worth saying, which is the common case.</summary>
    public string Status => Refusal switch
    {
        OfflineEditRefusal.SharedWithYou => "Shared with you - read-only until you're back online",
        OfflineEditRefusal.SharedWithOthers => "Shared with others - read-only until you're back online",
        _ => HasUnsentChanges ? "Waiting to sync" : string.Empty
    };

    public bool HasStatus => Status.Length > 0;

    /// <summary>A new note starts with one empty line, which is what the editor and the server expect.</summary>
    public static IReadOnlyList<NoteContentLineDto> EmptyContent => [new NoteContentLineDto(string.Empty, false, false)];
}
