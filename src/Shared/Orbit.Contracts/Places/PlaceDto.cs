using Orbit.Contracts;
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
/// <param name="IsShared">
/// False for the person who keeps it, true for anybody reading it through a share. None of the four
/// below are stored: they say how *this* caller relates to the place, worked out on every read - see
/// PlaceAccessResolver, and NoteDto, which carries the same four for the same reason.
/// </param>
/// <param name="SharedByUserName">Who handed it over, when <paramref name="IsShared"/> is true.</param>
/// <param name="AccessLevel">"ReadOnly" or "CanEdit" - always CanEdit for the person who keeps it.</param>
/// <param name="OriginalOwnerUserId">
/// Who keeps it, when this reader is not them. Null otherwise, which is what "mine" looks like.
/// </param>
/// <param name="IsSharedWithOthers">
/// The other side of <paramref name="IsShared"/>: somebody else holds access to this place. Only ever
/// meaningful to the person who keeps it.
/// </param>
/// <param name="IsPrivate">
/// Whether this place is sealed - and it is unless its owner said otherwise, which is the opposite
/// default from everything else here. See Orbit.Core.Places.Place.IsPrivate for why.
/// </param>
/// <param name="EncryptedContent">
/// The sealed name, description and point of a private place - see SealedPlace. Null for an open one,
/// whose three readable fields above carry the same thing in the clear.
/// </param>
public sealed record PlaceDto(
    Guid Id,
    string Name,
    string Description,
    EventLocationDto Where,
    string Colour,
    string Priority,
    IReadOnlyList<Guid> TaskListIds,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool IsShared = false,
    string? SharedByUserName = null,
    string AccessLevel = "CanEdit",
    Guid? OriginalOwnerUserId = null,
    bool IsSharedWithOthers = false,
    bool IsPrivate = false,
    EncryptedContentDto? EncryptedContent = null);
