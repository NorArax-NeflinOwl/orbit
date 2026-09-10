using System.Net;
using System.Net.Http.Json;
using System.Web;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Places;
using Orbit.Contracts.Sync;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Orbit's places endpoints, in memory. It carries the sealed half exactly as the real server does -
/// which for a place is nearly always there, since one is private unless its owner said otherwise. A
/// fake that quietly dropped it would answer every read with an empty place and prove the phone wrong
/// about work it had done correctly.
/// </summary>
internal sealed class FakePlacesServer : HttpMessageHandler
{
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<Guid, PlaceDto> _places = [];
    private readonly List<(Guid Id, DateTimeOffset DeletedAtUtc)> _tombstones = [];

    public FakePlacesServer(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public List<string> ReceivedRequests { get; } = [];

    public bool IsUnreachable { get; set; }

    public IReadOnlyCollection<PlaceDto> Places => _places.Values;

    /// <summary>
    /// A place already on the server, and an <em>open</em> one: a sealed place's words are ciphertext
    /// this fake has no key to make, and a test that wants one seals it through the phone's own
    /// repository instead.
    /// </summary>
    public PlaceDto AddPlace(
        string name, string address = "Piękna 1, Warszawa", double latitude = 52.2297, double longitude = 21.0122,
        bool isShared = false, string? sharedBy = null, string accessLevel = "CanEdit",
        bool isSharedWithOthers = false)
    {
        var now = _timeProvider.GetUtcNow();
        var place = new PlaceDto(
            Guid.NewGuid(), name, string.Empty, new EventLocationDto(address, latitude, longitude),
            Colour: string.Empty, Priority: "Normal", TaskListIds: [], now, now,
            isShared, sharedBy, accessLevel, isShared ? Guid.NewGuid() : null, isSharedWithOthers);

        _places[place.Id] = place;
        return place;
    }

    /// <summary>Takes one away the way the server does, so a delta says it is gone rather than falling silent.</summary>
    public void Forget(Guid placeId)
    {
        _places.Remove(placeId);
        _tombstones.Add((placeId, _timeProvider.GetUtcNow()));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        ReceivedRequests.Add($"{request.Method} {path}");

        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        if (path.EndsWith("/changes", StringComparison.Ordinal))
        {
            var since = DateTimeOffset.Parse(HttpUtility.ParseQueryString(request.RequestUri.Query)["since"]!);
            return Json(new ChangeFeedDto<PlaceDto>(
                [.. _places.Values.Where(place => place.UpdatedAtUtc >= since)],
                [.. _tombstones.Where(entry => entry.DeletedAtUtc >= since).Select(entry => entry.Id)],
                _timeProvider.GetUtcNow().UtcDateTime.ToString("O")));
        }

        return request.Method.Method switch
        {
            "POST" => await CreateAsync(request, cancellationToken),
            "PUT" => await SaveAsync(request, path, cancellationToken),
            "DELETE" => Delete(path),
            _ => Json(_places.Values.ToList())
        };
    }

    private async Task<HttpResponseMessage> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var asked = await request.Content!.ReadFromJsonAsync<SavePlaceRequest>(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var place = new PlaceDto(
            Guid.NewGuid(), asked!.Name, asked.Description, asked.Where, asked.Colour, asked.Priority,
            asked.TaskListIds ?? [], now, now,
            IsPrivate: asked.IsPrivate, EncryptedContent: asked.EncryptedContent);

        _places[place.Id] = place;
        return Json(place.Id);
    }

    private async Task<HttpResponseMessage> SaveAsync(
        HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var placeId = Guid.Parse(path.Split('/')[^1]);
        if (!_places.TryGetValue(placeId, out var stored))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var asked = await request.Content!.ReadFromJsonAsync<SavePlaceRequest>(cancellationToken);
        _places[placeId] = stored with
        {
            Name = asked!.Name,
            Description = asked.Description,
            Where = asked.Where,
            Colour = asked.Colour,
            Priority = asked.Priority,
            TaskListIds = asked.TaskListIds ?? [],
            IsPrivate = asked.IsPrivate,
            EncryptedContent = asked.EncryptedContent,
            UpdatedAtUtc = _timeProvider.GetUtcNow()
        };

        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private HttpResponseMessage Delete(string path)
    {
        Forget(Guid.Parse(path.Split('/')[^1]));
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private static HttpResponseMessage Json<TBody>(TBody body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    /// <summary>An HttpClient talking to this, with the base address the real clients are given.</summary>
    public HttpClient ToHttpClient() => new(this) { BaseAddress = new Uri("https://orbit.test/") };
}
