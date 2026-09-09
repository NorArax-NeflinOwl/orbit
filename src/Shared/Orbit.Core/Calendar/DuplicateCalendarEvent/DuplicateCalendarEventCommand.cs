using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.DuplicateCalendarEvent;

/// <summary>
/// A second appointment saying the same thing, at the same time and place. Only the owner's own - see
/// Orbit.Core.Notes.DuplicateNote.DuplicateNoteCommand, which says why.
/// </summary>
/// <param name="Name">What to call the copy, or null to keep the original's title - see Orbit.Contracts.DuplicateRequest.</param>
[ClientAction(ClientActionCategory.Save)]
public sealed record DuplicateCalendarEventCommand(Guid UserId, Guid Id, string? Name = null) : IRequest<Guid?>;
