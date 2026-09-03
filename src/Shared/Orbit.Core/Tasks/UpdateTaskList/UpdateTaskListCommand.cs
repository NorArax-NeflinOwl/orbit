using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.UpdateTaskList;

[ClientAction(ClientActionCategory.Edit)]
public sealed record UpdateTaskListCommand(
    Guid UserId, Guid Id, string Title, IReadOnlyList<TaskItem> Items, bool IsGroup, bool IsPrivate, EncryptedPayload? EncryptedContent,
    ItemPriority Priority = ItemPriority.Normal,
    /// <summary>
    /// Null leaves the stored description alone - see UpdateTaskRequest. An empty string clears it.
    /// </summary>
    string? Description = null,
    /// <summary>
    /// The entries whose categories the caller said nothing about, which keep whatever they are already
    /// filed under. The same rule <paramref name="Description"/> follows, and for the same reason: a
    /// client that has not learned about the field - the phone, an older tab - goes on saving lists
    /// without erasing what was written somewhere else. An entry that sends an empty list is clearing
    /// them, and is not in here.
    /// </summary>
    IReadOnlySet<Guid>? EntriesKeepingTheirCategories = null)
    : IRequest<EditOutcome>;
