using Orbit.Contracts;
namespace Orbit.Contracts.Tasks;

/// <summary>
/// IsGroup marks a list that gathers the lists its items link to - see Orbit.Core.Tasks.TaskList.IsGroup.
/// IsPrivate marks a list only its owner can read: Title and Items then travel empty and the real values
/// are sealed inside EncryptedContent, which the browser fills in and the server never opens.
/// Priority is one of "Low", "Normal", "High" - see Orbit.Core.Abstractions.ItemPriority.
/// </summary>
public sealed record UpdateTaskRequest(
    string Title, IReadOnlyList<TaskItemRequest> Items, bool IsGroup = false, bool IsPrivate = false,
    EncryptedContentDto? EncryptedContent = null, string Priority = "Normal");
