using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.SetTaskListPinned;

/// <summary>Pins or unpins one list - see TaskList.SetPinned for why this is its own command rather than part of an update.</summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record SetTaskListPinnedCommand(Guid UserId, Guid TaskListId, bool IsPinned) : IRequest<bool>;
