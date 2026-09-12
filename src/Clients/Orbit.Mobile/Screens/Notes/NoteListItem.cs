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
/// When it last changed, in as few words as it takes - see <see cref="LastChanged"/>. Already in the
/// reader's language and their calendar's culture rather than the phone's: reading an interface in
/// Polish and being told "Monday, March 3" is only half a translation.
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
/// <param name="Preview">
/// The note's first line, which is what the design puts under its name - the one thing that tells two
/// notes called the same thing apart, and the reason a list of names is not a list of notes. Empty for
/// a note that is only a title, and for a hidden one, whose words are sealed with everything else.
/// </param>
/// <param name="Priority">
/// How much it matters, when that is worth saying - the tag at the right-hand end of the name's line.
/// Empty for a Normal one, which is what everything is unless somebody said otherwise.
/// </param>
public sealed record NoteListItem(
    Guid LocalId, string Title, DateTimeOffset UpdatedAtUtc, bool HasUnsentChanges, OfflineEditRefusal Refusal,
    string Status = "", string Updated = "", bool IsPinned = false, bool IsSharedWithMe = false,
    bool IsHidden = false, string HiddenTitle = "Private", bool IsCopy = false,
    string Preview = "", string Priority = "", string PriorityValue = "Normal")
{
    /// <param name="tagColours">The account's tag colours by key - see LocalTagColourRepository.ColoursAsync. Null draws every tag plain.</param>
    public static NoteListItem From(
        LocalNote note, bool hasUnsentChanges, INetworkStatus networkStatus, bool privateItemsAreUnlocked,
        Translations translations, DateTimeOffset nowUtc, string hiddenTitle = "Private",
        IReadOnlyDictionary<string, string>? tagColours = null)
    {
        var refusal = OfflineEditPolicy.Evaluate(note, networkStatus);
        var isHidden = note.IsPrivate && !privateItemsAreUnlocked;

        return new(
            note.LocalId, note.IsSealed ? hiddenTitle : note.Title, note.UpdatedAtUtc, hasUnsentChanges, refusal,
            OfflineEditExplanation.For(note, refusal, hasUnsentChanges, translations),
            LastChanged.Describe(note.UpdatedAtUtc, nowUtc, translations),
            note.IsPinned, note.IsShared,
            IsHidden: isHidden, HiddenTitle: hiddenTitle,
            IsCopy: note.CopyOfLocalId is not null,
            Preview: isHidden || note.IsSealed ? string.Empty : FirstLineOf(note),
            Priority: Tasks.PriorityChoice.WorthSaying(note.Priority, translations),
            PriorityValue: note.Priority)
        {
            // Nothing about a hidden note, whose tags are sealed with everything else it says.
            Tags = isHidden || note.IsSealed ? Screens.Tags.TagChips.None : Screens.Tags.TagChips.For(note.AllTags, tagColours)
        };
    }

    /// <summary>The note's tags, in the row with its other marks - see TagChips.</summary>
    public Screens.Tags.TagChips Tags { get; init; } = Screens.Tags.TagChips.None;

    /// <summary>
    /// The first line that says anything. A note often opens with a blank line - the editor keeps one
    /// under the title - and showing that as the preview would leave the row looking like a note with
    /// nothing in it.
    /// </summary>
    private static string FirstLineOf(LocalNote note)
        => note.Content.FirstOrDefault(line => line.Text.Trim().Length > 0)?.Text.Trim() ?? string.Empty;

    /// <inheritdoc cref="Preview"/>
    public bool HasPreview => Preview.Length > 0;

    /// <inheritdoc cref="Priority"/>
    public bool HasPriority => Priority.Length > 0;

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
    /// Whether this row offers a pin at all. Every row does, except a hidden one, which offers nothing
    /// until it is unlocked.
    ///
    /// A note shared with this reader used to be left out: pinning moves a card on one person's page, so
    /// a recipient writing to the note's own flag would have rearranged its owner's list, and the server
    /// refused them for exactly that reason. Their answer goes on their own grant now
    /// (NoteShare.IsPinnedByRecipient), which is theirs to set and invisible to the owner.
    /// </summary>
    public bool CanBePinned => !IsHidden;

    /// <summary>A new note starts with one empty line, which is what the editor and the server expect.</summary>
    public static IReadOnlyList<NoteContentLineDto> EmptyContent => [new NoteContentLineDto(string.Empty, false, false)];
}
