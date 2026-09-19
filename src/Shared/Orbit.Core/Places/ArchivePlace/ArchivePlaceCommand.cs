using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.ArchivePlace;

/// <summary>
/// Puts one place away, or brings it back - see Place.Archive. The map then draws it under the archive
/// rather than among the places somebody is using, and deleting one is offered there and nowhere else.
///
/// Its own command rather than a field on the update, and one command for both directions rather than
/// two: ArchiveNoteCommand says why at length, and the reasons are the same here.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record ArchivePlaceCommand(Guid UserId, Guid PlaceId, bool IsArchived) : IRequest<bool>;
