using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.GetPlaces;

/// <inheritdoc cref="Orbit.Core.Notes.GetNotes.GetNotesQuery"/>
public sealed record GetPlacesQuery(Guid UserId, DateTimeOffset? UpdatedSinceUtc = null)
    : IRequest<IReadOnlyList<Place>>;
