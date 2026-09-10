using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Sharing;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// The sharing endpoints, four of each: offering a thing, and accepting one. Records every path called,
/// so a test can check an offer went to the endpoint its kind belongs to rather than any of the others.
///
/// An offer answers with a <see cref="ShareResultDto"/> and an acceptance with nothing, which is what
/// the real endpoints do - a fake that answered both the same way let a client that mishandled the body
/// pass.
/// </summary>
internal sealed class FakeShareServer : HttpMessageHandler
{
    private readonly List<string> _accepted = [];
    private readonly List<string> _offersRead = [];

    public bool IsUnreachable { get; set; }

    /// <summary>Refuses everything, as the server does for an offer already taken up or withdrawn.</summary>
    public bool RefusesEverything { get; set; }

    /// <summary>What an offer answers with: they had it already, so this is a reminder rather than news.</summary>
    public bool AlreadyShared { get; set; }

    /// <summary>The id the last offer was given, which is what the message to the recipient carries.</summary>
    public Guid LastShareId { get; private set; }

    /// <summary>The paths accepted, in order - "api/notes/shares/{id}/accept" and its three siblings.</summary>
    public IReadOnlyList<string> Accepted => _accepted;

    /// <summary>Offers this server says have already been taken up - see the /status endpoints.</summary>
    public HashSet<Guid> AlreadyTakenUp { get; } = [];

    /// <summary>
    /// The offers this server will read back, by share id - what GET api/shares/{kind}/{shareId}
    /// answers. Anything not here is 404, which is what the server says for an offer withdrawn, never
    /// made, or made to somebody else: it does not distinguish them, so neither does this.
    /// </summary>
    public Dictionary<Guid, ShareOfferDto> Offers { get; } = [];

    /// <summary>
    /// The kind segments read, in order - "note", "tasklist" and their siblings. A screen that read an
    /// offer under the wrong kind would find nothing on the real server, and this is what says so.
    /// </summary>
    public IReadOnlyList<string> OffersRead => _offersRead;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        _accepted.Add(path);

        if (RefusesEverything)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        // api/shares/{kind}/{shareId} - one offer, read by whoever it was made to. Its own group rather
        // than a fifth endpoint per section, which is why it is the one path here not under a section.
        if (path.StartsWith("api/shares/", StringComparison.Ordinal))
        {
            var segments = path.Split('/');
            _offersRead.Add(segments[2]);

            return Task.FromResult(Offers.TryGetValue(Guid.Parse(segments[3]), out var offer)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(offer) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        // api/{kind}/shares/{id}/status
        if (path.EndsWith("/status", StringComparison.Ordinal))
        {
            var shareId = Guid.Parse(path.Split('/')[^2]);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(AlreadyTakenUp.Contains(shareId))
            });
        }

        return Task.FromResult(path.EndsWith("/shares", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ShareResultDto(LastShareId = Guid.NewGuid(), AlreadyShared))
            }
            : new HttpResponseMessage(HttpStatusCode.NoContent));
    }

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
