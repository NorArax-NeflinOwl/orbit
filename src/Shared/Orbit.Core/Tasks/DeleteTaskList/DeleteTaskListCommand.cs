using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.DeleteTaskList;

/// <param name="DeleteTheListsItGathers">
/// Whether the lists this one's entries stand for go with it. False by default, which is what deleting
/// a list has always meant: a group list is a way of reading several lists together, and getting rid of
/// the reading is not the same as getting rid of what was being read. The caller asks the reader which
/// of the two they meant - see the confirmation on the task screens.
/// </param>
public sealed record DeleteTaskListCommand(Guid UserId, Guid Id, bool DeleteTheListsItGathers = false)
    : IRequest<bool>;
