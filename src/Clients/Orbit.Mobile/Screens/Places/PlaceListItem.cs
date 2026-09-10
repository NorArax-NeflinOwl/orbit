using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Places;

/// <summary>
/// One row of the places list. A view's worth of a <see cref="LocalPlace"/> - what to show, and the two
/// things the reader has to be told about it: whether the app is still holding a change, and whether it
/// can be changed at all right now. The same shape <see cref="Notes.NoteListItem"/> takes.
/// </summary>
/// <param name="Address">
/// Where it is, in words, which is what tells two places called the same thing apart. Empty for a point
/// nobody has named - a pin dropped on a field has coordinates and no street.
/// </param>
/// <param name="SharedBy">
/// Who handed it over, when somebody did. Said rather than left to a badge, because a place on this list
/// is either one the reader kept or one they were shown, and those are different things.
/// </param>
/// <param name="Priority">
/// How much it matters, when that is worth saying. Empty for a Normal one, which is what everything is
/// unless somebody said otherwise.
/// </param>
public sealed record PlaceListItem(
    Guid LocalId, string Name, string Address, DateTimeOffset UpdatedAtUtc, bool HasUnsentChanges,
    OfflineEditRefusal Refusal, string Status = "", string Updated = "", string SharedBy = "",
    bool IsSharedWithMe = false, bool IsSharedWithOthers = false, string Priority = "",
    string PriorityValue = "Normal", string Colour = "")
{
    public static PlaceListItem From(
        LocalPlace place, bool hasUnsentChanges, INetworkStatus networkStatus, Translations translations,
        DateTimeOffset nowUtc)
    {
        var refusal = OfflineEditPolicy.Evaluate(place, networkStatus);

        return new(
            place.LocalId, place.Name, place.Address, place.UpdatedAtUtc, hasUnsentChanges, refusal,
            OfflineEditExplanation.For(place, refusal, hasUnsentChanges, translations),
            LastChanged.Describe(place.UpdatedAtUtc, nowUtc, translations),
            SharedBy: place.IsShared ? place.SharedByUserName ?? string.Empty : string.Empty,
            IsSharedWithMe: place.IsShared,
            IsSharedWithOthers: place.IsSharedWithOthers,
            Priority: Tasks.PriorityChoice.WorthSaying(place.Priority, translations),
            PriorityValue: place.Priority,
            Colour: place.Colour);
    }

    /// <inheritdoc cref="Address"/>
    public bool HasAddress => Address.Length > 0;

    /// <inheritdoc cref="Priority"/>
    public bool HasPriority => Priority.Length > 0;

    /// <inheritdoc cref="SharedBy"/>
    public bool HasSharedBy => SharedBy.Length > 0;

    public bool IsEditable => Refusal is OfflineEditRefusal.None;

    public bool HasStatus => Status.Length > 0;

    /// <summary>
    /// What deleting it means, which is not the same thing for the two kinds of row: a place the reader
    /// keeps is destroyed, and one they were shown comes off their own map and stays on its owner's.
    /// </summary>
    public string DeleteLabel => IsSharedWithMe ? "Take it off my map" : "Delete";
}
