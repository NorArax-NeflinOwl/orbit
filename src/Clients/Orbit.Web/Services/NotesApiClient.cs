using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Notes;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/notes endpoints, keeping HTTP and JSON details out of the pages.
/// </summary>
public sealed class NotesApiClient
{
    private readonly HttpClient _httpClient;

    public NotesApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
        var response = await _httpClient.PostAsJsonAsync("api/notes", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateNoteAsync(Guid id, UpdateNoteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/notes/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteNoteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/notes/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
