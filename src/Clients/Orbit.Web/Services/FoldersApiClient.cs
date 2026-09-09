using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Folders;
using Orbit.Core.Folders;

namespace Orbit.Web.Services;

/// <summary>
/// The folders somebody made. Nothing here knows about the built-in three - they have no rows, and the
/// client draws them itself (see FolderState).
/// </summary>
public sealed class FoldersApiClient
{
    private readonly HttpClient _httpClient;

    public FoldersApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<FolderDto>> GetFoldersAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<FolderDto>>("api/folders", cancellationToken) ?? [];

    /// <summary>The scope is the page it is a tab on, and it is fixed here - see Orbit.Core.Folders.FolderScope.</summary>
    public async Task<FolderDto?> CreateFolderAsync(
        string name, FolderScope scope, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/folders", new CreateFolderRequest(name, scope.ToString()), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FolderDto>(cancellationToken: cancellationToken);
    }

    public async Task<bool> RenameFolderAsync(Guid id, string name, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/folders/{id}", new RenameFolderRequest(name), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<bool> DeleteFolderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/folders/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }
}
