using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Sync;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Orbit's notes API, in memory, behind a real <see cref="HttpMessageHandler"/>.
///
/// The sync tests drive the actual <see cref="Orbit.Mobile.Api.NotesClient"/> against this rather than a
/// mocked client, because half of what they are checking is the wire behaviour: which requests go out,
/// in what order, and what the client does with each status code. A mock of the client would assert
/// that the test's own assumptions were called.
/// </summary>
internal sealed class FakeNotesServer : HttpMessageHandler
{
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<Guid, NoteDto> _notes = [];
    private readonly List<(Guid Id, DateTimeOffset DeletedAtUtc)> _tombstones = [];

    public FakeNotesServer(TimeProvider timeProvider) => _timeProvider = timeProvider;

    /// <summary>Every request that reached it, as "METHOD /path" - the order is what replay is judged on.</summary>
    public List<string> ReceivedRequests { get; } = [];

    /// <summary>Set to make the next requests fail, so a test can take the network away mid-replay.</summary>
    public HttpStatusCode? ForcedFailure { get; set; }

    /// <summary>True while the server is simply unreachable, as it is to a phone with no signal.</summary>
    public bool IsUnreachable { get; set; }

    /// <summary>
    /// Set to refuse writes while still answering reads - a note shared read-only, which the reader may
    /// pull and may not change. <see cref="ForcedFailure"/> refuses everything, which is a different
    /// situation and cannot stand in for this one: it takes the change feed down as well.
    /// </summary>
    public HttpStatusCode? ForcedWriteFailure { get; set; }

    public IReadOnlyCollection<NoteDto> Notes => _notes.Values;

    public NoteDto AddNote(string title, bool isShared = false, bool isSharedWithOthers = false)
    {
        var now = _timeProvider.GetUtcNow();
        var note = new NoteDto(
            Guid.NewGuid(), title, [new NoteContentLineDto("Content", false, false)], false, null,
            now, now, isShared, isShared ? "someone" : null, "CanEdit", null, isSharedWithOthers);

        _notes[note.Id] = note;
        return note;
    }

    /// <summary>Swaps a note for an edited copy, so a test can set fields the API has no endpoint for.</summary>
    public void ReplaceForTest(NoteDto note) => _notes[note.Id] = note;

    public void DeleteNote(Guid id)
    {
        _notes.Remove(id);
        _tombstones.Add((id, _timeProvider.GetUtcNow()));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        ReceivedRequests.Add($"{request.Method} {path}");

        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        // Nobody else is ever in it here; EditLockTests covers the answer where somebody is.
        if (path.EndsWith("/lock", StringComparison.Ordinal))
        {
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (ForcedFailure is { } failure)
        {
            return new HttpResponseMessage(failure);
        }

        if (path.EndsWith("/changes", StringComparison.Ordinal))
        {
            return BuildChangeFeed(request.RequestUri.Query);
        }

        if (ForcedWriteFailure is { } writeFailure && request.Method.Method is "POST" or "PUT" or "DELETE")
        {
            return new HttpResponseMessage(writeFailure);
        }

        return request.Method.Method switch
        {
            "POST" => await CreateAsync(request, cancellationToken),
            "PUT" => await UpdateAsync(request, path, cancellationToken),
            "DELETE" => Delete(path),
            _ => Json(_notes.Values.ToList())
        };
    }

    private HttpResponseMessage BuildChangeFeed(string query)
    {
        var since = DateTimeOffset.Parse(HttpUtility.ParseQueryString(query)["since"]!);
        var changed = _notes.Values.Where(note => note.UpdatedAtUtc >= since).ToList();
        var deleted = _tombstones.Where(entry => entry.DeletedAtUtc >= since).Select(entry => entry.Id).ToList();

        return Json(new ChangeFeedDto<NoteDto>(
            changed, deleted, _timeProvider.GetUtcNow().UtcDateTime.ToString("O")));
    }

    private async Task<HttpResponseMessage> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await ReadAsync<CreateNoteRequest>(request, cancellationToken);
        var created = AddNote(body!.Title);
        _notes[created.Id] = created with
        {
            Content = body.Content, IsPrivate = body.IsPrivate, Priority = body.Priority,
            // Stored as the real endpoint stores it: a private note's words are only here, so a fake
            // that dropped it would answer the next pull with an empty note and look like data loss.
            EncryptedContent = body.EncryptedContent
        };
        return Json(created.Id, HttpStatusCode.Created);
    }

    private async Task<HttpResponseMessage> UpdateAsync(HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var id = ReadId(path);
        if (!_notes.TryGetValue(id, out var existing))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var body = await ReadAsync<UpdateNoteRequest>(request, cancellationToken);
        _notes[id] = existing with
        {
            Title = body!.Title,
            Content = body.Content,
            IsPrivate = body.IsPrivate,
            EncryptedContent = body.EncryptedContent,
            // Stored by the real endpoint, and a fake that dropped it would hide the very thing this
            // was written for: an update that carried no priority looked exactly like one that did.
            Priority = body.Priority,
            UpdatedAtUtc = _timeProvider.GetUtcNow()
        };

        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private HttpResponseMessage Delete(string path)
    {
        var id = ReadId(path);
        if (!_notes.ContainsKey(id))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        DeleteNote(id);
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private static Guid ReadId(string path) => Guid.Parse(path.Split('/')[^1]);

    private static async Task<T?> ReadAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
        => JsonSerializer.Deserialize<T>(
            await request.Content!.ReadAsStringAsync(cancellationToken),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static HttpResponseMessage Json<T>(T payload, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode) { Content = JsonContent.Create(payload) };

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
