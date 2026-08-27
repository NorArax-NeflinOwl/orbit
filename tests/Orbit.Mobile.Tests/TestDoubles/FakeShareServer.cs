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

    public bool IsUnreachable { get; set; }

    /// <summary>Refuses everything, as the server does for an offer already taken up or withdrawn.</summary>
    public bool RefusesEverything { get; set; }

    /// <summary>What an offer answers with: they had it already, so this is a reminder rather than news.</summary>
    public bool AlreadyShared { get; set; }

    /// <summary>The id the last offer was given, which is what the message to the recipient carries.</summary>
    public Guid LastShareId { get; private set; }

    /// <summary>The paths accepted, in order - "api/notes/shares/{id}/accept" and its three siblings.</summary>
    public IReadOnlyList<string> Accepted => _accepted;

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

        return Task.FromResult(path.EndsWith("/shares", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ShareResultDto(LastShareId = Guid.NewGuid(), AlreadyShared))
            }
            : new HttpResponseMessage(HttpStatusCode.NoContent));
    }

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
