using Orbit.Contracts;
namespace Orbit.Contracts.Tasks;

/// <summary>
/// IsGroup marks a list that gathers the lists its items link to - see Orbit.Core.Tasks.TaskList.IsGroup.
/// IsPrivate marks a list only its owner can read: Title and Items then travel empty and the real values
/// are sealed inside EncryptedContent, which the browser fills in and the server never opens.
/// Priority is one of "Low", "Normal", "High" - see Orbit.Core.Abstractions.ItemPriority.
/// </summary>
    /// <param name="Description">
    /// What the list is about, under its title. <b>Null means "not provided", and leaves whatever is
    /// stored alone</b>; an empty string means "cleared". The distinction is what lets a client that
    /// has not learned about this field yet - the phone, an older browser tab - go on saving lists
    /// without erasing a description written somewhere else. An older client sends nothing, which
    /// arrives as null, which changes nothing.
    /// </param>
public sealed record CreateTaskRequest(
    string Title, IReadOnlyList<TaskItemRequest> Items, bool IsGroup = false, bool IsPrivate = false,
    EncryptedContentDto? EncryptedContent = null, string Priority = "Normal", string? Description = null);
