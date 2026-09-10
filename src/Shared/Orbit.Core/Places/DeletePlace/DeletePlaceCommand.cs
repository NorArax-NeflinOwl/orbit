using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.DeletePlace;

/// <summary>
/// False back when there is nothing of this user's under that id - a place somebody else owns and one
/// that never existed answer the same way, because telling them apart would say whether an id exists.
/// No ClientAction: deleting is not one of the categories the log stream tags, which DeleteNoteCommand
/// beside it says by carrying none either.
/// </summary>
public sealed record DeletePlaceCommand(Guid UserId, Guid Id) : IRequest<bool>;
