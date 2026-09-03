using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts;
using Orbit.Contracts.Sharing;
using Orbit.Contracts.Tasks;
using Orbit.Core.Abstractions;
using Orbit.Web.Services.Logging;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/tasks endpoints, keeping HTTP and JSON details out of the pages.
///
/// Private lists are sealed and opened here rather than in each page (see PrivateContentSealer), so the
/// checklist view, the overview, the dashboard and the calendar all receive a readable TaskDto without
/// any of them knowing that a private one arrives empty with its real items encrypted.
/// </summary>
public sealed class TasksApiClient
{
    private readonly HttpClient _httpClient;
    private readonly Translations? _translations;
    private readonly ILogger _logger;
    private readonly PrivateContentSealer? _privateContentSealer;

    /// <summary>Shown in place of a private list nobody can open any more - see PrivateContentSealer.OpenAsync.</summary>
    public const string UnreadableTaskListTitle = "Unreadable - encrypted with an older key";

    // logger, sealer and translations default to absent rather than being required, so existing call
    // sites (including every test that constructs this with just an HttpClient) keep compiling
    // unchanged; only the DI-resolved instance registered in Program.cs logs, handles private lists, or
    // speaks the reader's language.
    public TasksApiClient(HttpClient httpClient, ILogger<TasksApiClient>? logger = null, PrivateContentSealer? privateContentSealer = null, Translations? translations = null)
    {
        _httpClient = httpClient;
        _translations = translations;
        _logger = logger ?? NullLogger<TasksApiClient>.Instance;
        _privateContentSealer = privateContentSealer;
    }

    /// <summary>Pins or unpins one list. Returns false when it isn't the caller's to pin - see SetTaskListPinnedCommandHandler.</summary>
    public async Task<bool> SetPinnedAsync(Guid taskListId, bool isPinned, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/tasks/{taskListId}/pinned", new SetTaskListPinnedRequest(isPinned), cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Builds the shelf this list's work needs - one entry per distinct thing it calls for, each starting
    /// at nothing - and points the list at it. Returns the new inventory's id.
    /// </summary>
    public async Task<Guid?> GenerateInventoryAsync(Guid taskListId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/tasks/{taskListId}/inventory", content: null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken);
    }

    /// <summary>Points a task list at the inventory its work is measured against, or at none.</summary>
    public async Task<bool> LinkInventoryAsync(Guid taskListId, Guid? inventoryId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/tasks/{taskListId}/inventory", new LinkTaskListToInventoryRequest(inventoryId), cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// What this list's work costs against that inventory, or null when no inventory has been chosen -
    /// there is no question to answer then, which is not the same as an answer of "nothing".
    /// </summary>
    public async Task<TaskListStockCheckDto?> GetStockCheckAsync(Guid taskListId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/tasks/{taskListId}/stock-check", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TaskListStockCheckDto>(cancellationToken);
    }

    /// <summary>Puts what is short onto the inventory's restock list. Returns how many entries were added.</summary>
    public async Task<int> RaiseStockShortfallsAsync(Guid taskListId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/tasks/{taskListId}/stock-check/shortfalls", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RaiseStockShortfallsResultDto>(cancellationToken);
        return result?.AddedCount ?? 0;
    }

    /// <summary>
    /// Settles the finished errands on a restock list - each fills its shelf item and then leaves the
    /// list. Answers how many were settled, and does nothing at all for an ordinary list, so the
    /// checklist screen can ask on opening any of them.
    /// </summary>
    public async Task<int> ReconcileRestockingAsync(Guid taskListId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/tasks/{taskListId}/restocking/reconcile", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RestockReconciliationResultDto>(cancellationToken);
        return result?.SettledCount ?? 0;
    }

    /// <summary>
    /// What each inventory errand on this list is about: the shelf item behind it, and any other list
    /// asking for the same thing. Empty for a list holding none.
    /// </summary>
    public async Task<IReadOnlyList<InventoryReferenceDto>> GetInventoryReferencesAsync(
        Guid taskListId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<InventoryReferenceDto>>(
            $"api/tasks/{taskListId}/inventory-references", cancellationToken) ?? [];

    /// <summary>
    /// Says the whole restock list is done: crosses off what is left of it and brings its inventory up
    /// to the levels it is meant to hold. Answers how many shelf items moved.
    /// </summary>
    public async Task<int> FinishRestockingAsync(Guid taskListId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/tasks/{taskListId}/restocking/finished", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<FinishRestockingResultDto>(cancellationToken);
        return result?.ToppedUpCount ?? 0;
    }

    public async Task<IReadOnlyList<TaskDto>> GetTaskListsAsync(CancellationToken cancellationToken = default)
    {
        var taskLists = await _httpClient.GetFromJsonAsync<List<TaskDto>>("api/tasks", cancellationToken) ?? [];

        var opened = new List<TaskDto>(taskLists.Count);
        foreach (var taskList in taskLists)
        {
            opened.Add(await OpenIfPrivateAsync(taskList, cancellationToken));
        }

        return opened;
    }

    public async Task<TaskDto?> GetTaskListByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/tasks/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var taskList = await response.Content.ReadFromJsonAsync<TaskDto>(cancellationToken: cancellationToken);
        return taskList is null ? null : await OpenIfPrivateAsync(taskList, cancellationToken);
    }

    /// <summary>
    /// Hands back an ordinary list unchanged, and a private one with its real title and items put back
    /// in place, including the completion flag the server could no longer work out for itself. A list
    /// that can't be opened says so in its title rather than throwing.
    /// </summary>
    private async Task<TaskDto> OpenIfPrivateAsync(TaskDto taskList, CancellationToken cancellationToken)
    {
        if (!taskList.IsPrivate || taskList.EncryptedContent is not { } encryptedContent || _privateContentSealer is null)
        {
            return taskList;
        }

        var content = await _privateContentSealer.OpenAsync<SealedTaskList>(encryptedContent, cancellationToken);
        if (content is null)
        {
            return taskList with { Title = Translated(UnreadableTaskListTitle) };
        }

        return taskList with
        {
            Title = content.Title,
            Items = content.Items,
            // Recomputed here for the same reason the domain derives it: the server saw no items to
            // derive it from, so what it sent back is meaningless for a private list.
            IsCompleted = content.Items.Count > 0 && content.Items.All(item => item.IsCompleted)
        };
    }

    /// <summary>Mirrors NotesApiClient.SealIfPrivateAsync - see its comment.</summary>
    private async Task<(string Title, IReadOnlyList<TaskItemRequest> Items, EncryptedContentDto? EncryptedContent)> SealIfPrivateAsync(
        string title, IReadOnlyList<TaskItemRequest> items, bool isPrivate, CancellationToken cancellationToken)
    {
        if (!isPrivate)
        {
            return (title, items, null);
        }

        if (_privateContentSealer is null)
        {
            throw new InvalidOperationException("This TasksApiClient was built without a PrivateContentSealer, so it can't save a private list.");
        }

        var sealedItems = items
            .Select(item => new TaskItemDto(
                Guid.Empty, item.Description, item.DueDateUtc, item.IsCompleted,
                // Sealed from whichever shape the caller used, and written into the new field: the
                // single one carries only the first, and a private list would silently lose the rest.
                LinkedTaskListId: null,
                item.OverdueNotificationChannel, item.RemindDaily, item.DailyReminderNotificationChannel,
                item.DailyReminderTimeOfDay,
                LinkedTaskListIds: item.AllLinkedTaskListIds))
            .ToList();
        var encryptedContent = await _privateContentSealer.SealAsync(new SealedTaskList(title, sealedItems), cancellationToken);
        return (string.Empty, [], encryptedContent);
    }

    public async Task<Guid> CreateTaskListAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var (title, items, encryptedContent) = await SealIfPrivateAsync(
            request.Title, request.Items, request.IsPrivate, cancellationToken);
        request = request with { Title = title, Items = items, EncryptedContent = encryptedContent };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/tasks", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var id = await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
            _logger.LogActionCompleted(ClientActionCategory.Save, "Create task list");
            return id;
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.Save, "Create task list", exception);
            throw;
        }
    }

    /// <summary>Mirrors NotesApiClient.UpdateNoteAsync - see its comment for what NotFound/Locked mean here.</summary>
    public async Task<EditOutcome> UpdateTaskListAsync(Guid id, UpdateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var (title, items, encryptedContent) = await SealIfPrivateAsync(
            request.Title, request.Items, request.IsPrivate, cancellationToken);
        request = request with { Title = title, Items = items, EncryptedContent = encryptedContent };

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/tasks/{id}", request, cancellationToken);
            var outcome = await ToEditOutcomeAsync(response, cancellationToken);
            if (outcome.Kind == EditOutcomeKind.Success)
            {
                _logger.LogActionCompleted(ClientActionCategory.Edit, "Update task list");
            }

            return outcome;
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.Edit, "Update task list", exception);
            throw;
        }
    }

    /// <summary>Moves one item out of sourceTaskListId and into targetTaskListId - see MoveTaskItemCommandHandler.</summary>
    public async Task<EditOutcome> MoveTaskItemAsync(
        Guid sourceTaskListId, Guid itemId, Guid targetTaskListId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"api/tasks/{sourceTaskListId}/items/{itemId}/move", new MoveTaskItemRequest(targetTaskListId), cancellationToken);
            var outcome = await ToEditOutcomeAsync(response, cancellationToken);
            if (outcome.Kind == EditOutcomeKind.Success)
            {
                _logger.LogActionCompleted(ClientActionCategory.Edit, "Move task item");
            }

            return outcome;
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.Edit, "Move task item", exception);
            throw;
        }
    }

    /// <summary>
    /// Puts an event on a task list as an entry that points at it. One request rather than a whole-list
    /// save, so nothing else on the list is at risk - see LinkCalendarEventToTaskListCommand.
    /// </summary>
    public async Task<EditOutcome> LinkCalendarEventAsync(
        Guid taskListId, Guid calendarEventId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"api/tasks/{taskListId}/items/calendar-event", new LinkCalendarEventRequest(calendarEventId), cancellationToken);
            var outcome = await ToEditOutcomeAsync(response, cancellationToken);
            if (outcome.Kind == EditOutcomeKind.Success)
            {
                _logger.LogActionCompleted(ClientActionCategory.Edit, "Link a calendar event to a task list");
            }

            return outcome;
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.Edit, "Link a calendar event to a task list", exception);
            throw;
        }
    }

    /// <summary>Mirrors NotesApiClient.AcquireNoteLockAsync - see its comment.</summary>
    public async Task<EditOutcome> AcquireTaskListLockAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/tasks/{id}/lock", content: null, cancellationToken);
        return await ToEditOutcomeAsync(response, cancellationToken);
    }

    /// <summary>Mirrors NotesApiClient.ReleaseNoteLockAsync - see its comment.</summary>
    public async Task ReleaseTaskListLockAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tasks/{id}/lock", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<EditOutcome> ToEditOutcomeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return EditOutcome.NotFound;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflict = await response.Content.ReadFromJsonAsync<LockConflictDto>(cancellationToken: cancellationToken);
            return EditOutcome.LockedBy(conflict?.LockedByUserName ?? Translated("another user"));
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            var refusal = await response.Content.ReadFromJsonAsync<RefusalDto>(cancellationToken: cancellationToken);
            return EditOutcome.RefusedBecause(refusal?.Message ?? Translated("This was shared with you to read, not to change."));
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // The server explains a refusal in the body (see InvalidRequestExceptionHandler); throwing
            // that away left the reader with "something went wrong" and no way to find out what.
            var refusal = await response.Content.ReadFromJsonAsync<RefusalDto>(cancellationToken: cancellationToken);
            return EditOutcome.RefusedBecause(refusal?.Message ?? Translated("Orbit refused that change."));
        }

        response.EnsureSuccessStatusCode();
        return EditOutcome.Success;
    }

    public async Task DeleteTaskListAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tasks/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Offers a copy of taskListId to recipientUserId under the given access level ("ReadOnly", "Share",
    /// or "CanEdit" - see Orbit.Core.Abstractions.ShareAccessLevel), or null if taskListId doesn't exist,
    /// isn't accessible to the caller, or the caller isn't allowed to share it (see
    /// ShareTaskListCommandHandler). <see cref="ShareResultDto.AlreadyShared"/> is true when taskListId
    /// was already offered to recipientUserId - the returned ShareId is the existing offer's, not a new
    /// one, so the caller can send it again as a reminder instead of creating a duplicate. Notifying the
    /// recipient (an encrypted chat message carrying that id) is a separate step - see
    /// EncryptedChatMessageSender. Mirrors CalendarApiClient.ShareCalendarEventAsync.
    /// </summary>
    public async Task<ShareResultDto?> ShareTaskListAsync(
        Guid taskListId, Guid recipientUserId, string accessLevel = "ReadOnly", CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"api/tasks/{taskListId}/shares", new ShareTaskListRequest(recipientUserId, accessLevel), cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ShareResultDto>(cancellationToken: cancellationToken);
            _logger.LogActionCompleted(ClientActionCategory.ShareElement, "Share task list");
            return result;
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.ShareElement, "Share task list", exception);
            throw;
        }
    }

    /// <summary>Returns false instead of throwing when shareId doesn't exist or wasn't offered to the caller.</summary>
    public async Task<bool> AcceptTaskListShareAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/tasks/shares/{shareId}/accept", content: null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>Whether shareId has already been accepted, or null if it doesn't exist or wasn't offered to the caller.</summary>
    public async Task<bool?> GetTaskListShareStatusAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/tasks/shares/{shareId}/status", cancellationToken);
        // Refused reads the same as absent from here: an account that has not unlocked sharing cannot
        // be told whether an offer was taken, and "no such offer" is the honest answer to give it. This
        // is asked in passing while a conversation is opened - see Chat - and left throwing, one 403
        // took the whole conversation down.
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// The reader's language for text this client substitutes in - English when there is no
    /// Translations to ask, which is every test that builds this client by hand. Translated here
    /// rather than where it is rendered, because by then a stand-in title is indistinguishable from
    /// a real one the reader wrote.
    /// </summary>
    private string Translated(string english) => _translations?[english] ?? english;
}
