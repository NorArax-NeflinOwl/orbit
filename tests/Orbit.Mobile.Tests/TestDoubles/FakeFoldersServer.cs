using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Orbit.Contracts.Folders;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// The folders half of the API, in memory - the four calls FoldersClient makes, answered the way the
/// real endpoints answer them.
///
/// It has to keep what it is given, which is the whole reason it exists rather than a stub answering
/// every request with one body. Folders are the one thing the phone syncs with **no change feed**: the
/// synchroniser asks for all of them and reconciles, so a folder missing from the answer is a folder
/// that has been deleted (see FolderSynchronizer). A stub answering an empty list therefore deleted the
/// folder the test had just made, one line after the create it had answered with the wrong shape
/// entirely - a JsonException that escaped the synchroniser's own catch. See
/// [[fakes-must-refuse-what-the-server-refuses]]: a fake that does less than the server lets a client
/// that is wrong look right, and here it made a client that was right look wrong.
/// </summary>
internal sealed class FakeFoldersServer : HttpMessageHandler
{
    private readonly Dictionary<Guid, FolderDto> _folders = [];
    private readonly TimeProvider _timeProvider;

    public FakeFoldersServer(TimeProvider timeProvider) => _timeProvider = timeProvider;

    /// <summary>Every request that reached it, as "METHOD /path".</summary>
    public List<string> ReceivedRequests { get; } = [];

    public IReadOnlyCollection<FolderDto> Folders => _folders.Values;

    /// <summary>A folder made elsewhere, as the next pull would bring one down.</summary>
    public FolderDto AddFolder(string name, string scope)
    {
        var now = _timeProvider.GetUtcNow();
        var folder = new FolderDto(Guid.NewGuid(), name, scope, now, now);
        _folders[folder.Id] = folder;
        return folder;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        ReceivedRequests.Add($"{request.Method} {path}");

        return request.Method.Method switch
        {
            "POST" => Json(Create(await ReadAsync<CreateFolderRequest>(request, cancellationToken)), HttpStatusCode.Created),
            "PUT" => Rename(path, await ReadAsync<RenameFolderRequest>(request, cancellationToken)),
            "DELETE" => Delete(path),
            _ => Json(_folders.Values.ToList())
        };
    }

    private FolderDto Create(CreateFolderRequest? request)
    {
        var folder = AddFolder(request!.Name, request.Scope);
        return folder;
    }

    private HttpResponseMessage Rename(string path, RenameFolderRequest? request)
    {
        var id = ReadId(path);
        if (!_folders.TryGetValue(id, out var stored))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        _folders[id] = stored with { Name = request!.Name, UpdatedAtUtc = _timeProvider.GetUtcNow() };
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private HttpResponseMessage Delete(string path)
        => _folders.Remove(ReadId(path))
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : new HttpResponseMessage(HttpStatusCode.NotFound);

    private static Guid ReadId(string path) => Guid.Parse(path.Split('/')[^1]);

    private static async Task<T?> ReadAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
        => JsonSerializer.Deserialize<T>(
            await request.Content!.ReadAsStringAsync(cancellationToken),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static HttpResponseMessage Json<T>(T payload, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode) { Content = JsonContent.Create(payload) };

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.test/") };
}
