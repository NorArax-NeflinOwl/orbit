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
    IReadOnlySet<Guid>? EntriesKeepingTheirCategories = null,
    /// <summary>
    /// The entries that said nothing about the product they describe, which keep whatever they already
    /// ask for. The same rule the categories follow one line up, and for the same reason: the phone and
    /// every older tab save lists without knowing an entry can describe a product, and a save about
    /// something else must not empty what was written on the web.
    /// </summary>
    IReadOnlySet<Guid>? EntriesKeepingTheirProduct = null,
    /// <summary>
    /// The entries that said nothing about their own description, which keep the one they already
    /// carry. The third field to follow this rule, for the third time the same reason: a client written
    /// before an entry could carry a description saves lists without one, and that save must not wipe
    /// what somebody typed on the web. An entry that sends an empty string is clearing it.
    /// </summary>
    IReadOnlySet<Guid>? EntriesKeepingTheirNotes = null,
    /// <summary>
    /// The entries that said nothing about what they wait for, which keep the steps they already have.
    /// The fourth field to follow this rule, and the reason has not changed: a client written before
    /// steps existed saves lists without them, and that save must not undo the order somebody arranged
    /// somewhere else. An entry that sends an empty list is clearing them.
    /// </summary>
    IReadOnlySet<Guid>? EntriesKeepingTheirSteps = null,
    /// <summary>
    /// The entries that said nothing about how they are drawn or how much they matter, which keep the
    /// answers they already carry. The fifth field to follow this rule; the two travel together because
    /// a client either knows about both or about neither. An entry that sends "Normal" and an empty
    /// colour is saying so.
    /// </summary>
    IReadOnlySet<Guid>? EntriesKeepingTheirLook = null,
    /// <summary>
    /// What the reader says about whether the list is finished - see TaskListCompletion. Null means the
    /// caller said nothing and the stored answer stands - the same rule the three fields above follow,
    /// and the reason is the same: the phone saves lists without knowing this exists.
    /// </summary>
    TaskListCompletion? Completion = null,
    /// <summary>
    /// The entries that said nothing about the ways they are done by, which keep the ways they already
    /// have - see TaskItem.Alternatives. The sixth field to follow this rule, for the usual reason: the
    /// phone builds already installed save lists without knowing ways exist, and such a save must not
    /// wipe the ways written on the web. An entry that sends an empty list is clearing them.
    /// </summary>
    IReadOnlySet<Guid>? EntriesKeepingTheirAlternatives = null,
    /// <summary>
    /// The entries that said nothing about what they are the same thing as, nor how much of it they
    /// need - see TaskItem.ReferencesTaskItemId and TaskItem.RequiredQuantity. The seventh field to follow
    /// this rule, for the phone builds already installed.
    /// </summary>
    IReadOnlySet<Guid>? EntriesKeepingTheirReference = null,
    /// <summary>Null leaves the stored tags alone - see UpdateTaskRequest.Tags. An empty list clears them.</summary>
    IReadOnlyList<string>? Tags = null)
    : IRequest<EditOutcome>;
