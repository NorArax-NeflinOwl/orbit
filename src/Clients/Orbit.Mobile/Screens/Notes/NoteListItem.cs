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
public sealed record NoteListItem(
    Guid LocalId, string Title, DateTimeOffset UpdatedAtUtc, bool HasUnsentChanges, OfflineEditRefusal Refusal,
    string Status = "", string Updated = "", bool IsHidden = false, string HiddenTitle = "Private",
    bool IsPinned = false, bool IsShared = false)
{
    public static NoteListItem From(
        LocalNote note, bool hasUnsentChanges, INetworkStatus networkStatus, bool privateItemsAreUnlocked,
        Translations translations, string hiddenTitle = "Private")
    {
        var refusal = OfflineEditPolicy.Evaluate(note, networkStatus);

        return new(
            note.LocalId, note.Title, note.UpdatedAtUtc, hasUnsentChanges, refusal,
            OfflineEditExplanation.For(refusal, hasUnsentChanges, translations),
            translations.Format(
                "Updated {0}", note.UpdatedAtUtc.ToLocalTime().ToString("g", translations.DisplayCulture)),
            IsHidden: note.IsPrivate && !privateItemsAreUnlocked, HiddenTitle: hiddenTitle,
            IsPinned: note.IsPinned, IsShared: note.IsShared);
    }

    /// <summary>
    /// Only the owner may pin, so a note that arrived through somebody else's share shows its pin state
    /// without offering to change it - see SetNotePinnedCommandHandler. Unlike editing, this does not
    /// depend on being online: nobody else can be pinning the same note.
    /// </summary>
    public bool CanBePinned => !IsShared;

    /// <summary>What the row shows instead of the title while it is hidden.</summary>
    public string DisplayTitle => IsHidden ? HiddenTitle : Title;

    /// <summary>Only a hidden row offers to unlock; every other row opens.</summary>
    public bool CanBeOpened => !IsHidden;

    public bool IsEditable => Refusal is OfflineEditRefusal.None;

    public bool HasStatus => Status.Length > 0;

    /// <summary>A new note starts with one empty line, which is what the editor and the server expect.</summary>
    public static IReadOnlyList<NoteContentLineDto> EmptyContent => [new NoteContentLineDto(string.Empty, false, false)];
}
