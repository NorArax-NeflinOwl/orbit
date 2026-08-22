using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Tasks;
using Orbit.Core.Abstractions;
using Orbit.Web.Services.Logging;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/tasks endpoints, keeping HTTP and JSON details out of the pages.
/// </summary>
public sealed class TasksApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    // logger defaults to a no-op instance rather than being required, so existing call sites (including
    // every test that constructs this with just an HttpClient) keep compiling unchanged; only the
    // DI-resolved instance registered in Program.cs actually logs anywhere.
    public TasksApiClient(HttpClient httpClient, ILogger<TasksApiClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger ?? NullLogger<TasksApiClient>.Instance;
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

    public async Task UpdateTaskListAsync(Guid id, UpdateTaskRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/tasks/{id}", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            _logger.LogActionCompleted(ClientActionCategory.Edit, "Update task list");
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.Edit, "Update task list", exception);
            throw;
        }
    }

    public async Task DeleteTaskListAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tasks/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
