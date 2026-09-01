using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Orbit.Contracts.Sync;
using Orbit.Contracts.Tasks;
using Orbit.Core.Tasks;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Orbit's task-list endpoints, in memory - the counterpart of <see cref="FakeNotesServer"/>, so the
/// second entity type on the sync spine is exercised the same way the first one is.
/// </summary>
internal sealed class FakeTasksServer : HttpMessageHandler
{
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<Guid, TaskDto> _taskLists = [];
    private readonly List<(Guid Id, DateTimeOffset DeletedAtUtc)> _tombstones = [];

    public FakeTasksServer(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public List<string> ReceivedRequests { get; } = [];

    public bool IsUnreachable { get; set; }

    /// <summary>Set to hold every request open, so a test can make two runs genuinely overlap.</summary>
    public TaskCompletionSource? HoldRequestsUntil { get; set; }

    public IReadOnlyCollection<TaskDto> TaskLists => _taskLists.Values;

    /// <summary>What the stock check answers, or null for "no warehouse chosen".</summary>
    public TaskListStockCheckDto? StockCheck { get; set; }

    /// <summary>What generating a warehouse hands back, or null when there was nothing to build.</summary>
    public Guid? GeneratedWarehouseId { get; set; } = Guid.NewGuid();

    public int RaisedShortfallCount { get; set; }

    /// <summary>What bringing the list and the warehouse back into step reports having moved.</summary>
    public StockReconciliationResultDto Reconciliation { get; set; } = new(0, 0);

    /// <summary>How many times it was actually asked for - the panel used to only re-read instead.</summary>
    public int ReconciliationsAsked { get; private set; }

    /// <summary>How many products bringing the whole warehouse up to its minimum moved.</summary>
    public int ToppedUpCount { get; set; }

    /// <summary>How many times the shelf was asked to be topped up in one go.</summary>
    public int RestockingsFinished { get; private set; }

    /// <summary>How often the screen asked for the finished errands to be settled - see ReconcileRestockingAsync.</summary>
    public int RestockingsSettled { get; private set; }

    /// <summary>What the settle answers with. Nothing settled unless a test says otherwise.</summary>
    public int SettledCount { get; set; }

    public int SettledToppedUpCount { get; set; }

    /// <summary>The warehouse a list was last pointed at.</summary>
    public Guid? LinkedWarehouseId { get; private set; }

    /// <summary>Named apart from the contract so this fake does not depend on its property name.</summary>
    private sealed record LinkTaskItemToWarehouseBody(Guid? WarehouseId);

    public IReadOnlyList<TaskItemDto> ItemsIn(Guid taskListId)
        => _taskLists.TryGetValue(taskListId, out var taskList) ? taskList.Items : [];

    public TaskDto AddTaskList(string title, bool isShared = false, bool isSharedWithOthers = false)
    {
        var now = _timeProvider.GetUtcNow();
        var taskList = new TaskDto(
            Guid.NewGuid(), title, [], false, false, false, null, now, now,
            isShared, isShared ? "someone" : null, "CanEdit", null, "Normal", "New", false, isSharedWithOthers);

        _taskLists[taskList.Id] = taskList;
        return taskList;
    }

    /// <summary>Swaps a list for an edited copy, so a test can set fields the API has no endpoint for.</summary>
    public void ReplaceForTest(TaskDto taskList) => _taskLists[taskList.Id] = taskList;

    public void DeleteTaskList(Guid id)
    {
        _taskLists.Remove(id);
        _tombstones.Add((id, _timeProvider.GetUtcNow()));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        ReceivedRequests.Add($"{request.Method} {path}");

        if (HoldRequestsUntil is { } held)
        {
            await held.Task;
        }

        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        // Nobody else is ever in it here; EditLockTests covers the answer where somebody is.
        if (path.EndsWith("/lock", StringComparison.Ordinal))
        {
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (path.EndsWith("/changes", StringComparison.Ordinal))
        {
            var since = DateTimeOffset.Parse(HttpUtility.ParseQueryString(request.RequestUri.Query)["since"]!);
            return Json(new ChangeFeedDto<TaskDto>(
                _taskLists.Values.Where(list => list.UpdatedAtUtc >= since).ToList(),
                _tombstones.Where(entry => entry.DeletedAtUtc >= since).Select(entry => entry.Id).ToList(),
                _timeProvider.GetUtcNow().UtcDateTime.ToString("O")));
        }

        // api/tasks/{id}/stock-check and its two siblings - see StockCheckPanel.
        if (path.EndsWith("/stock-check", StringComparison.Ordinal))
        {
            return StockCheck is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : Json(StockCheck);
        }

        if (path.EndsWith("/stock-check/shortfalls", StringComparison.Ordinal))
        {
            return Json(new RaiseStockShortfallsResultDto(RaisedShortfallCount));
        }

        if (path.EndsWith("/stock-check/reconciliation", StringComparison.Ordinal))
        {
            ReconciliationsAsked++;
            return Json(Reconciliation);
        }

        if (path.EndsWith("/restocking/finished", StringComparison.Ordinal))
        {
            RestockingsFinished++;
            return Json(new FinishRestockingResultDto(ToppedUpCount));
        }

        if (path.EndsWith("/restocking/reconcile", StringComparison.Ordinal))
        {
            RestockingsSettled++;
            return Json(new RestockReconciliationResultDto(SettledToppedUpCount, SettledCount));
        }

        if (path.EndsWith("/inventory", StringComparison.Ordinal))
        {
            return GeneratedWarehouseId is { } generated
                ? Json(generated)
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if (path.EndsWith("/warehouse", StringComparison.Ordinal))
        {
            var body = await ReadAsync<LinkTaskItemToWarehouseBody>(request, cancellationToken);
            LinkedWarehouseId = body?.WarehouseId;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        // api/tasks/{sourceId}/items/{itemId}/move
        if (path.EndsWith("/move", StringComparison.Ordinal))
        {
            var segments = path.Split('/');
            return await MoveItemAsync(
                request, Guid.Parse(segments[^4]), Guid.Parse(segments[^2]), cancellationToken);
        }

        return request.Method.Method switch
        {
            "POST" => await CreateAsync(request, cancellationToken),
            "PUT" => await UpdateAsync(request, path, cancellationToken),
            "DELETE" => Delete(path),
            _ => Json(_taskLists.Values.ToList())
        };
    }

    /// <summary>
    /// As MoveTaskItemCommandHandler does it: the entry leaves one list and arrives in the other, and
    /// both lists count as changed so a delta pull brings them both back.
    /// </summary>
    private async Task<HttpResponseMessage> MoveItemAsync(
        HttpRequestMessage request, Guid sourceId, Guid itemId, CancellationToken cancellationToken)
    {
        var body = await ReadAsync<MoveTaskItemRequest>(request, cancellationToken);
        if (!_taskLists.TryGetValue(sourceId, out var source)
            || !_taskLists.TryGetValue(body!.TargetTaskListId, out var target)
            || source.Items.FirstOrDefault(item => item.Id == itemId) is not { } moved)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var movedAt = _timeProvider.GetUtcNow();
        _taskLists[sourceId] = source with
        {
            Items = [.. source.Items.Where(item => item.Id != itemId)],
            UpdatedAtUtc = movedAt
        };
        _taskLists[target.Id] = target with { Items = [.. target.Items, moved], UpdatedAtUtc = movedAt };
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private async Task<HttpResponseMessage> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await ReadAsync<CreateTaskRequest>(request, cancellationToken);
        var created = AddTaskList(body!.Title);
        _taskLists[created.Id] = created with
        {
            Items = ToDtos(body.Items), IsGroup = body.IsGroup, IsPrivate = body.IsPrivate,
            // Stored as the real endpoint stores it: a private list's title and entries are only here,
            // so a fake that dropped it would answer the next pull with an empty list.
            EncryptedContent = body.EncryptedContent,
            Priority = body.Priority,
            // As the real endpoint stores it: null means "not provided", and a private list keeps none
            // at all - see Orbit.Core.Tasks.TaskList.
            Description = body.IsPrivate ? string.Empty : body.Description ?? string.Empty
        };
        return Json(created.Id, HttpStatusCode.Created);
    }

    private async Task<HttpResponseMessage> UpdateAsync(HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var id = ReadId(path);
        if (!_taskLists.TryGetValue(id, out var existing))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var body = await ReadAsync<UpdateTaskRequest>(request, cancellationToken);
        _taskLists[id] = existing with
        {
            Title = body!.Title,
            Items = ToDtos(body.Items),
            // Sent on every update and stored by the real endpoint - a fake that dropped it made
            // "this list is now a group list" look like a client that had not sent it. The priority
            // went the same way afterwards: the push carried it, the pull brought back the fake's own
            // "Normal" over the top, and the phone looked like it had never sent one.
            IsGroup = body.IsGroup,
            IsPrivate = body.IsPrivate,
            EncryptedContent = body.EncryptedContent,
            Priority = body.Priority,
            // <inheritdoc cref="CreateAsync"/> - and null here keeps what was stored rather than
            // clearing it, which is the whole point of the field being nullable.
            Description = body.IsPrivate
                ? string.Empty
                : body.Description ?? existing.Description,
            UpdatedAtUtc = _timeProvider.GetUtcNow()
        };

        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private HttpResponseMessage Delete(string path)
    {
        var id = ReadId(path);
        if (!_taskLists.ContainsKey(id))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        DeleteTaskList(id);
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Keeps the id it was sent, and mints one only for an entry that has none - what the real endpoint
    /// does (see TaskEndpoints.ToDomainItem). Minting unconditionally would let a client that dropped
    /// entry ids pass its tests and lose them against a real server.
    /// </summary>
    private static IReadOnlyList<TaskItemDto> ToDtos(IReadOnlyList<TaskItemRequest> items)
        => items.Select(item => new TaskItemDto(
            item.Id ?? Guid.NewGuid(), item.Description, item.DueDateUtc, item.IsCompleted,
            // Whichever shape the client sent, answered in both - what the real endpoint does, so a
            // client reading only the old field still works against this fake. See TaskEndpoints.ToDto.
            item.AllLinkedTaskListIds.Count > 0 ? item.AllLinkedTaskListIds[0] : null,
            item.OverdueNotificationChannel, item.RemindDaily, item.DailyReminderNotificationChannel,
            item.DailyReminderTimeOfDay, item.Kind, item.Location, item.LinkedCalendarEventId,
            // Kept only for an Inventory entry, which is TaskItem's own rule - a fake that kept it for
            // every kind would let a client sending the wrong kind pass, and the real server would cut
            // the errand loose from its product.
            item.Kind == nameof(TaskItemKind.Inventory) ? item.LinkedInventoryItemId : null,
            item.AllLinkedTaskListIds)).ToList();

    private static Guid ReadId(string path) => Guid.Parse(path.Split('/')[^1]);

    private static async Task<T?> ReadAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
        => JsonSerializer.Deserialize<T>(
            await request.Content!.ReadAsStringAsync(cancellationToken),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static HttpResponseMessage Json<T>(T payload, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode) { Content = JsonContent.Create(payload) };

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
