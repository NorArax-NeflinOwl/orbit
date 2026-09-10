using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Folders;

namespace Orbit.Mobile.Api;

/// <summary>
/// The folders half of the API - the tabs somebody made. Only the synchroniser calls this; screens read
/// the local database, as they do for everything else (see info/orbit-maui-plan.md §5).
///
/// There is no change feed for folders and there does not need to be one: an account has a handful of
/// them, they carry a name and nothing else, and asking for all of them costs less than the cursor
/// would. The browser's own client does the same.
/// </summary>
public sealed class FoldersClient
{
    private readonly HttpClient _httpClient;

    public FoldersClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<IReadOnlyList<FolderDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<FolderDto>>("api/folders", cancellationToken) ?? [];

    /// <summary>
    /// Makes one. The whole folder comes back rather than its id, which is what lets a phone that made
    /// it offline check the server agreed to the name it already drew.
    /// </summary>
    public async Task<FolderDto?> CreateAsync(CreateFolderRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/folders", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FolderDto>(cancellationToken);
    }

    public async Task<WriteOutcome> RenameAsync(
        Guid folderId, RenameFolderRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync($"api/folders/{folderId}", request, cancellationToken);
        return ReadOutcome(response);
    }

    /// <summary>Takes the tab away and leaves everything under it - see IFolderRepository.DeleteAsync.</summary>
    public async Task<WriteOutcome> DeleteAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync($"api/folders/{folderId}", cancellationToken);
        return ReadOutcome(response);
    }

    /// <summary>
    /// The same three answers every write on this phone gets - see WriteOutcome, and NotesClient, which
    /// reads them off the same status codes.
    /// </summary>
    private static WriteOutcome ReadOutcome(HttpResponseMessage response)
        => response.StatusCode switch
        {
            HttpStatusCode.Conflict or HttpStatusCode.Forbidden => WriteOutcome.Refused,
            HttpStatusCode.NotFound => WriteOutcome.Gone,
            HttpStatusCode.BadRequest => WriteOutcome.Rejected,
            _ => response.IsSuccessStatusCode ? WriteOutcome.Applied : throw Failed(response)
        };

    private static HttpRequestException Failed(HttpResponseMessage response)
        => new($"The server answered {(int)response.StatusCode} to a folder request.");
}
