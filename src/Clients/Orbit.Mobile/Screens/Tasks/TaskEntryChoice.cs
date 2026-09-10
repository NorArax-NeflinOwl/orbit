namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// One entry of a list, as something to pick out of a list of them - what the "Waits for" picker offers
/// and what the row under it names. Mirrors <see cref="TaskListChoice"/>, which is the same idea one
/// level up: that one picks a list, this one picks an entry on the list already open.
///
/// Its own type rather than the DTO, because a picker needs one readable line and an entry carries a
/// great deal more - and rather than TaskListChoice, because an entry is not a list and an id that
/// could be either is an id nobody can check.
/// </summary>
/// <param name="Description">
/// What the entry is called. An entry saved with no words yet reads as "…", the same stand-in the
/// browser's own picker uses - a blank row in a picker is a row nobody can choose on purpose.
/// </param>
public sealed record TaskEntryChoice(Guid Id, string Description)
{
    public static TaskEntryChoice For(Guid id, string description)
        => new(id, description.Trim().Length > 0 ? description.Trim() : "…");
}
