using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Sync;
using Orbit.Contracts.Tasks;

namespace Orbit.Mobile.Api;

/// <summary>
/// The task lists half of the API. Only the synchroniser calls this - screens read the local database,
/// exactly as they do for notes.
/// </summary>
public sealed class TasksClient
{
    private readonly HttpClient _httpClient;

    public TasksClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>
    /// What changed since <paramref name="cursor"/>, and what was deleted. A null cursor asks for
    /// everything, which is what a device that has never synced needs.
    /// </summary>
    public async Task<ChangeFeedDto<TaskDto>> GetChangesAsync(string? cursor, CancellationToken cancellationToken = default)
    {
        var since = cursor ?? DateTimeOffset.MinValue.UtcDateTime.ToString("O");
        return await _httpClient.GetFromJsonAsync<ChangeFeedDto<TaskDto>>(
            $"api/tasks/changes?since={Uri.EscapeDataString(since)}", cancellationToken)
            ?? new ChangeFeedDto<TaskDto>([], [], since);
    }

    public async Task<Guid> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/tasks", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken);
    }

    public async Task<WriteOutcome> UpdateAsync(
        Guid taskListId, UpdateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tasks/{taskListId}", request, cancellationToken);
        return ReadOutcome(response);
    }

    public async Task<WriteOutcome> DeleteAsync(Guid taskListId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tasks/{taskListId}", cancellationToken);
        // A list already gone is the outcome the caller wanted, not a failure to retry.
        return response.StatusCode is HttpStatusCode.NotFound ? WriteOutcome.Applied : ReadOutcome(response);
    }

    /// <summary>
    /// Unlike a delete, a 404 here is not the outcome the caller wanted: the server answers one for a
    /// list that does not exist and for one the caller does not own alike, and only its owner may pin a
    /// list. Either way nothing queued against it can ever succeed, which is what Gone already says.
    /// </summary>
    public async Task<WriteOutcome> SetPinnedAsync(
        Guid taskListId, bool isPinned, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/tasks/{taskListId}/pinned", new SetTaskListPinnedRequest(isPinned), cancellationToken);
        return ReadOutcome(response);
    }

    /// <summary>
    /// Anything not named here - a server error, a gateway timeout - throws, so the queued change stays
    /// queued and is tried again. Only a refusal the server will repeat is worth giving up on.
    /// </summary>
    private static WriteOutcome ReadOutcome(HttpResponseMessage response)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.Conflict:
                return WriteOutcome.Refused;
            case HttpStatusCode.NotFound:
                return WriteOutcome.Gone;
            default:
                response.EnsureSuccessStatusCode();
                return WriteOutcome.Applied;
        }
    }
}
