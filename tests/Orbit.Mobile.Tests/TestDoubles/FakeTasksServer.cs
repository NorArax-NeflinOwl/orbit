using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Orbit.Contracts.Sync;
using Orbit.Contracts.Tasks;

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

        if (path.EndsWith("/changes", StringComparison.Ordinal))
        {
            var since = DateTimeOffset.Parse(HttpUtility.ParseQueryString(request.RequestUri.Query)["since"]!);
            return Json(new ChangeFeedDto<TaskDto>(
                _taskLists.Values.Where(list => list.UpdatedAtUtc >= since).ToList(),
                _tombstones.Where(entry => entry.DeletedAtUtc >= since).Select(entry => entry.Id).ToList(),
                _timeProvider.GetUtcNow().UtcDateTime.ToString("O")));
        }

        return request.Method.Method switch
        {
            "POST" => await CreateAsync(request, cancellationToken),
            "PUT" => await UpdateAsync(request, path, cancellationToken),
            "DELETE" => Delete(path),
            _ => Json(_taskLists.Values.ToList())
        };
    }

    private async Task<HttpResponseMessage> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await ReadAsync<CreateTaskRequest>(request, cancellationToken);
        var created = AddTaskList(body!.Title);
        _taskLists[created.Id] = created with { Items = ToDtos(body.Items), IsPrivate = body.IsPrivate };
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
            IsPrivate = body.IsPrivate,
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
            item.Id ?? Guid.NewGuid(), item.Description, item.DueDateUtc, item.IsCompleted, item.LinkedTaskListId,
            item.OverdueNotificationChannel, item.RemindDaily, item.DailyReminderNotificationChannel,
            item.DailyReminderTimeOfDay)).ToList();

    private static Guid ReadId(string path) => Guid.Parse(path.Split('/')[^1]);

    private static async Task<T?> ReadAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
        => JsonSerializer.Deserialize<T>(
            await request.Content!.ReadAsStringAsync(cancellationToken),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static HttpResponseMessage Json<T>(T payload, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode) { Content = JsonContent.Create(payload) };

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
