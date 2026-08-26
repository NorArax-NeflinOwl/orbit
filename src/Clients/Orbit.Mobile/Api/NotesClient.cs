using System.Net.Http.Json;
using Orbit.Contracts.Notes;

namespace Orbit.Mobile.Api;

/// <summary>
/// Reads the signed-in user's notes.
///
/// Deliberately thin, and deliberately talking straight to the API: from phase 2 every screen reads
/// from the local database and the sync layer keeps that current (see info/orbit-maui-plan.md §5). This
/// exists to prove the authenticated round trip end to end, and is the seam that sync replaces.
/// </summary>
public sealed class NotesClient
{
    private readonly HttpClient _httpClient;

    public NotesClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<IReadOnlyList<NoteDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<NoteDto>>("api/notes", cancellationToken) ?? [];
}
