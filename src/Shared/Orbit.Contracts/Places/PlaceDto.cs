using Orbit.Contracts.Calendar;

namespace Orbit.Contracts.Places;

/// <summary>
/// Somewhere on the map worth keeping - see Orbit.Core.Places.Place, which says why it is its own thing
/// rather than an appointment's address or somebody's position.
/// </summary>
/// <param name="Where">
/// The address as a reader reads it and the point a map draws it at. The calendar's own shape, because
/// it holds the same sort of thing - see EventLocationDto.
/// </param>
/// <param name="Colour">
/// What colour its pin takes, or empty for whatever a place is drawn in. The same shape a calendar
/// event's colour and a task entry's take.
/// </param>
/// <param name="Priority">One of "Low", "Normal", "High" - see Orbit.Core.Abstractions.ItemPriority.</param>
/// <param name="TaskListIds">The lists it belongs to, in order - see Place.TaskListIds.</param>
public sealed record PlaceDto(
    Guid Id,
    string Name,
    string Description,
    EventLocationDto Where,
    string Colour,
    string Priority,
    IReadOnlyList<Guid> TaskListIds,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
