using Orbit.Contracts.Calendar;

namespace Orbit.Contracts.Places;

/// <summary>
/// What a place is, as the form saves it - one request for making one and for changing one, because a
/// place is small enough that the two say exactly the same thing.
///
/// Nothing here is "not provided": the form holds every field and sends every field, so a missing one is
/// a client that meant to clear it. That is the opposite of the rule a task entry's newer fields follow,
/// and deliberately so - those exist because two clients disagree about what an entry carries, and
/// nothing but this form has ever written a place.
/// </summary>
public sealed record SavePlaceRequest(
    string Name,
    EventLocationDto Where,
    string Description = "",
    string Colour = "",
    string Priority = "Normal",
    IReadOnlyList<Guid>? TaskListIds = null);
