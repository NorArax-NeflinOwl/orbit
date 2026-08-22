using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Notes;
using Orbit.Core.Abstractions;
using Orbit.Web.Services.Logging;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/notes endpoints, keeping HTTP and JSON details out of the pages.
/// </summary>
public sealed class NotesApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    // logger defaults to a no-op instance rather than being required, so existing call sites (including
    // every test that constructs this with just an HttpClient) keep compiling unchanged; only the
    // DI-resolved instance registered in Program.cs actually logs anywhere.
    public NotesApiClient(HttpClient httpClient, ILogger<NotesApiClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger ?? NullLogger<NotesApiClient>.Instance;
    }

    public async Task<IReadOnlyList<NoteDto>> GetNotesAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<NoteDto>>("api/notes", cancellationToken) ?? [];

    public async Task<NoteDto?> GetNoteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/notes/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NoteDto>(cancellationToken: cancellationToken);
    }

    public async Task<Guid> CreateNoteAsync(CreateNoteRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/notes", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var id = await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
            _logger.LogActionCompleted(ClientActionCategory.Save, "Create note");
            return id;
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.Save, "Create note", exception);
            throw;
        }
    }

    public async Task UpdateNoteAsync(Guid id, UpdateNoteRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/notes/{id}", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            _logger.LogActionCompleted(ClientActionCategory.Edit, "Update note");
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.Edit, "Update note", exception);
            throw;
        }
    }

    public async Task DeleteNoteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/notes/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
