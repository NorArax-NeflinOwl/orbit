using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;

namespace Orbit.Core.Places;

/// <summary>
/// Somewhere on the map worth keeping, on its own account rather than because something happens there.
///
/// Orbit knew two kinds of place before this and neither was one: an appointment's, which exists because
/// the appointment does and goes when it goes, and a person's shared position, which is where somebody
/// is this minute. Neither answers "the good bakery", "where we park", "the flat we are viewing on
/// Saturday" - a place somebody wants to keep and be taken back to, with no date on it and nobody
/// standing there.
///
/// Owned by exactly one user for its whole life, like a note. It carries what it is called, what the
/// reader wrote about it, where it is, what colour its pin takes, how much it matters, and the lists it
/// belongs to - the last being how a place joins the work it is about: the bakery belongs to the
/// shopping list, the site belongs to the renovation.
/// </summary>
public sealed class Place
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>What it is called - the one field a place cannot be without.</summary>
    public string Name { get; private set; }

    /// <summary>What the reader wrote about it, or empty. The longer half of the two, like a task entry's notes.</summary>
    public string Description { get; private set; }

    /// <summary>
    /// Where it is: the address as somebody reads it, and the point the map draws it at. The same shape
    /// an appointment's place takes (see <see cref="EventLocation"/>) rather than a second one - a place
    /// is a place, and two records of what one is would be two ways for a pin to end up somewhere else.
    /// </summary>
    public EventLocation Where { get; private set; }

    /// <summary>
    /// What colour its pin is drawn in, as the reader chose it, or empty for "whatever a place is drawn
    /// in". The same shape a calendar event's colour and a task entry's take.
    /// </summary>
    public string Colour { get; private set; }

    /// <summary>How much it matters - see <see cref="ItemPriority"/>. What sorts a list of them.</summary>
    public ItemPriority Priority { get; private set; }

    /// <summary>
    /// The task lists this place belongs to, in the order they were added. How a place joins the work it
    /// is about, and the reason it is not simply a pin: a bakery on the shopping list is a different
    /// thing from a bakery nobody has filed anywhere.
    ///
    /// Not validated - a list deleted afterwards leaves an id pointing at nothing, and a reader treats
    /// that as "a list nobody here can see", the same way a task entry's links are treated.
    /// </summary>
    public IReadOnlyList<Guid> TaskListIds { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// False for the owner, true for anybody reading this place through a share - see PlaceAccessResolver.
    ///
    /// Not stored, like a note's: sharing grants access to this row rather than making a copy, so what
    /// these three say is how the *caller* relates to it, worked out fresh on every read. The same row
    /// reads differently for the person who kept it than for the person they handed it to.
    /// </summary>
    public bool IsShared { get; private set; }

    /// <summary>The owner's login, whenever <see cref="IsShared"/> is true. Null otherwise.</summary>
    public string? SharedByUserName { get; private set; }

    /// <summary>
    /// True when somebody else holds accepted access to this place. Only ever meaningful to its owner -
    /// the other side of the same relationship is <see cref="IsShared"/>.
    /// </summary>
    public bool IsSharedWithOthers { get; private set; }

    /// <summary>The caller's access - always CanEdit for the owner, and whatever their share grants otherwise.</summary>
    public ShareAccessLevel AccessLevel { get; private set; } = ShareAccessLevel.CanEdit;

    /// <summary>Stamps how the caller relates to this place. Called by PlaceAccessResolver; never stored.</summary>
    public void SetAccessContext(bool isShared, string? sharedByUserName, ShareAccessLevel accessLevel)
    {
        IsShared = isShared;
        SharedByUserName = sharedByUserName;
        AccessLevel = accessLevel;
    }

    /// <inheritdoc cref="IsSharedWithOthers"/>
    public void SetSharedWithOthers(bool isSharedWithOthers) => IsSharedWithOthers = isSharedWithOthers;

    private Place(
        Guid id, Guid userId, string name, string description, EventLocation where, string colour,
        ItemPriority priority, IReadOnlyList<Guid>? taskListIds,
        DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
    {
        Id = id;
        UserId = userId;
        Name = name.Trim();
        Description = description.Trim();
        Where = where;
        Colour = colour.Trim();
        Priority = priority;
        // Distinct and in order: naming the same list twice is one belonging written twice.
        TaskListIds = taskListIds is null ? [] : [.. taskListIds.Distinct()];
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    /// <summary>
    /// A new place. The limits are checked here rather than in the constructor, which
    /// <see cref="FromPersistence"/> also uses: a row already stored fits by definition, and refusing one
    /// on the way back out would make it unreadable rather than telling anybody anything - the rule every
    /// other aggregate here follows.
    /// </summary>
    public static Place Create(
        Guid userId, string name, string description, EventLocation where, string colour = "",
        ItemPriority priority = ItemPriority.Normal, IReadOnlyList<Guid>? taskListIds = null)
    {
        Refuse(name, description, where, colour);
        var nowUtc = DateTimeOffset.UtcNow;
        return new Place(
            Guid.NewGuid(), userId, name, description, where, colour, priority, taskListIds, nowUtc, nowUtc);
    }

    /// <summary>Rebuilds one from a stored row, with none of the checks above - see <see cref="Create"/>.</summary>
    public static Place FromPersistence(
        Guid id, Guid userId, string name, string description, EventLocation where, string colour,
        ItemPriority priority, IReadOnlyList<Guid>? taskListIds,
        DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
        => new(id, userId, name, description, where, colour, priority, taskListIds, createdAtUtc, updatedAtUtc);

    /// <summary>
    /// Everything a reader can change about a place. One method rather than one per field, because the
    /// form saves the lot: a place is small enough that partial updates would be more machinery than the
    /// thing being updated.
    /// </summary>
    public void Update(
        string name, string description, EventLocation where, string colour, ItemPriority priority,
        IReadOnlyList<Guid>? taskListIds)
    {
        Refuse(name, description, where, colour);
        Name = name.Trim();
        Description = description.Trim();
        Where = where;
        Colour = colour.Trim();
        Priority = priority;
        TaskListIds = taskListIds is null ? [] : [.. taskListIds.Distinct()];
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// The same place under a new name and a new id - what Duplicate makes. The copy is its owner's own
    /// from the moment it exists, which is why this takes the owner rather than reading it: duplicating
    /// is only ever done by somebody who may already read it.
    /// </summary>
    public Place CopyFor(Guid userId, string name)
        => Create(userId, name, Description, Where, Colour, Priority, TaskListIds);

    private static void Refuse(string name, string description, EventLocation where, string colour)
    {
        StoredTextLimits.OrRefuse(name, StoredTextLimits.Title, "place's name");
        StoredTextLimits.OrRefuse(description, StoredTextLimits.EventDescription, "place's description");
        StoredTextLimits.OrRefuse(where.Address ?? string.Empty, StoredTextLimits.Address, "place's address");
        StoredTextLimits.OrRefuse(colour, StoredTextLimits.Color, "place's colour");
    }
}
