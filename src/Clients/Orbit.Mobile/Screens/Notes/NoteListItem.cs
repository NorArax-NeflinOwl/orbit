using Orbit.Contracts.Notes;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Notes;

/// <summary>
/// One row of the notes list. A view's worth of a <see cref="LocalNote"/> - what to show, and the two
/// things the user has to be told about it: whether the app is still holding a change, and whether it
/// can be changed at all right now.
/// </summary>
/// <param name="Updated">
/// When it last changed, already in the reader's language and their calendar's culture rather than the
/// phone's - reading an interface in Polish and being told "Monday, March 3" is only half a translation.
/// </param>
/// <param name="IsHidden">
/// A private note while private things are locked. The row still appears - a note vanishing from the
/// list would look like it had been deleted - but says nothing about itself until it is unlocked.
/// </param>
/// <param name="Title">
/// What the note is called. A private note this device could not open has none to show - its title is
/// sealed with the rest of it - so the row falls back to what a hidden one says and the note itself
/// explains why when it is opened (see NoteDetailViewModel).
/// </param>
/// <param name="IsCopy">
/// Taken from another note to be written on with no connection. Two rows with the same title are
/// otherwise indistinguishable, and the reader has no way of telling which one they have been writing
/// in - so the copy says so.
/// </param>
public sealed record NoteListItem(
    Guid LocalId, string Title, DateTimeOffset UpdatedAtUtc, bool HasUnsentChanges, OfflineEditRefusal Refusal,
    string Status = "", string Updated = "", bool IsPinned = false, bool IsSharedWithMe = false,
    bool IsHidden = false, string HiddenTitle = "Private", bool IsCopy = false)
{
    public static NoteListItem From(
        LocalNote note, bool hasUnsentChanges, INetworkStatus networkStatus, bool privateItemsAreUnlocked,
        Translations translations, string hiddenTitle = "Private")
    {
        var refusal = OfflineEditPolicy.Evaluate(note, networkStatus);

        return new(
            note.LocalId, note.IsSealed ? hiddenTitle : note.Title, note.UpdatedAtUtc, hasUnsentChanges, refusal,
            OfflineEditExplanation.For(note, refusal, hasUnsentChanges, translations),
            translations.Format(
                "Updated {0}", note.UpdatedAtUtc.ToLocalTime().ToString("g", translations.DisplayCulture)),
            note.IsPinned, note.IsShared,
            IsHidden: note.IsPrivate && !privateItemsAreUnlocked, HiddenTitle: hiddenTitle,
            IsCopy: note.CopyOfLocalId is not null);
    }

    /// <summary>What the row shows instead of the title while it is hidden.</summary>
    public string DisplayTitle => IsHidden ? HiddenTitle : Title;

    /// <summary>Only a hidden row offers to unlock; every other row opens.</summary>
    public bool CanBeOpened => !IsHidden;

    /// <summary>
    /// Whether the card's three dots have anything behind them. Every note does: one this reader owns
    /// can be deleted, and one shared with them can be taken off their own list.
    /// </summary>
    public bool HasCardMenu => true;

    public bool IsEditable => Refusal is OfflineEditRefusal.None;

    public bool HasStatus => Status.Length > 0;

    /// <summary>
    /// Whether this row offers a pin at all. Only the owner may pin - pinning moves a card on one
    /// person's page, so a recipient pinning a note shared with them would be rearranging its owner's
    /// list - and a hidden row offers nothing until it is unlocked.
    /// </summary>
    public bool CanBePinned => !IsHidden && !IsSharedWithMe;

    /// <summary>A new note starts with one empty line, which is what the editor and the server expect.</summary>
    public static IReadOnlyList<NoteContentLineDto> EmptyContent => [new NoteContentLineDto(string.Empty, false, false)];
}
