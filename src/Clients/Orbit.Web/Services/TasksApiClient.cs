using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Tasks;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/tasks endpoints, keeping HTTP and JSON details out of the pages.
/// </summary>
public sealed class TasksApiClient
{
    private readonly HttpClient _httpClient;

    public TasksApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<TaskDto>> GetTaskListsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<TaskDto>>("api/tasks", cancellationToken) ?? [];

    public async Task<TaskDto?> GetTaskListByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/tasks/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TaskDto>(cancellationToken: cancellationToken);
    }

    public async Task<Guid> CreateTaskListAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/tasks", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateTaskListAsync(Guid id, UpdateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tasks/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTaskListAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tasks/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
