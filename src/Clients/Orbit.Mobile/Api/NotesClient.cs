using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Sync;

namespace Orbit.Mobile.Api;

/// <summary>
/// The notes half of the API. Only the synchroniser calls this - screens read the local database, and
/// the sync layer is what keeps the two in step (see info/orbit-maui-plan.md §5).
/// </summary>
public sealed class NotesClient
{
    private readonly HttpClient _httpClient;

    public NotesClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<IReadOnlyList<NoteDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<NoteDto>>("api/notes", cancellationToken) ?? [];

    /// <summary>
    /// What changed since <paramref name="cursor"/>, and what was deleted. A null cursor asks for
    /// everything, which is what a device that has never synced needs.
    /// </summary>
    public async Task<ChangeFeedDto<NoteDto>> GetChangesAsync(string? cursor, CancellationToken cancellationToken = default)
    {
        var since = cursor ?? DateTimeOffset.MinValue.UtcDateTime.ToString("O");
        return await _httpClient.GetFromJsonAsync<ChangeFeedDto<NoteDto>>(
            $"api/notes/changes?since={Uri.EscapeDataString(since)}", cancellationToken)
            ?? new ChangeFeedDto<NoteDto>([], [], since);
    }

    /// <summary>The id the server assigned, which is what makes the local note reachable from anywhere else.</summary>
    public async Task<Guid> CreateAsync(CreateNoteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/notes", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken);
    }

    public async Task<WriteOutcome> UpdateAsync(Guid noteId, UpdateNoteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/notes/{noteId}", request, cancellationToken);
        return ReadOutcome(response);
    }

    public async Task<WriteOutcome> DeleteAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/notes/{noteId}", cancellationToken);
        // A note already gone is the outcome the caller wanted, not a failure to retry.
        return response.StatusCode is HttpStatusCode.NotFound ? WriteOutcome.Applied : ReadOutcome(response);
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
