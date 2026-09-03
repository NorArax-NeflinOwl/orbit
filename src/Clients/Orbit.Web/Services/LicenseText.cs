namespace Orbit.Web.Services;

/// <summary>
/// The licence Orbit ships under, as words rather than as a link somewhere else. Read from the copy
/// served beside the app (see Orbit.Web.csproj, which links the repository's own LICENSE into wwwroot),
/// so the page can never say something the file does not.
///
/// Its own HttpClient, pointed at the origin the browser loaded the page from rather than at the API:
/// this is a static file sitting next to index.html, and it needs no token to read.
/// </summary>
public sealed class LicenseText
{
    private readonly HttpClient _httpClient;
    private string? _text;

    public LicenseText(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// The licence, or null when it could not be read - which the page says out loud rather than
    /// showing an empty box that looks like a licence granting nothing.
    /// </summary>
    public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (_text is not null)
        {
            return _text;
        }

        try
        {
            _text = await _httpClient.GetStringAsync("LICENSE.txt", cancellationToken);
            return _text;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
