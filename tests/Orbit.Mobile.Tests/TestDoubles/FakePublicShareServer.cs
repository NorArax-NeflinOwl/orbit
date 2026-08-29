using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Config;
using Orbit.Contracts.Sharing;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Links anyone can read a thing by. Holds one token per item, as the server does - asking for a link
/// that exists returns the same one rather than minting a second, because a second would leave the
/// first working and revoking would then stop only one of them.
/// </summary>
internal sealed class FakePublicShareServer : HttpMessageHandler
{
    private readonly Dictionary<string, string> _tokens = [];

    public bool IsUnreachable { get; set; }

    /// <summary>Where this deployment says its browser client lives - empty means it has not said.</summary>
    public string WebAddress { get; set; } = "https://orbit.example";

    /// <summary>Refuses to make one, as the server does for an item that is not the caller's.</summary>
    public bool RefusesToCreate { get; set; }

    public int LinksCreated { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        var path = request.RequestUri!.AbsolutePath.TrimStart('/');

        if (path == "api/config/client-flags")
        {
            return Json(new ClientFlagsDto(false, string.Empty, WebAddress));
        }

        if (request.Method == HttpMethod.Post)
        {
            if (RefusesToCreate)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            LinksCreated++;
            var created = Guid.NewGuid().ToString("N");
            _tokens[KeyOf(request)] = created;
            return Json(new PublicShareLinkDto(created, DateTimeOffset.UtcNow));
        }

        if (request.Method == HttpMethod.Delete)
        {
            _tokens.Remove(path["api/share-links/".Length..]);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }

        return _tokens.TryGetValue(path["api/share-links/".Length..], out var token)
            ? Json(new PublicShareLinkDto(token, DateTimeOffset.UtcNow))
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    }

    /// <summary>A create names the item in its body; a read and a revoke name it in the path.</summary>
    private static string KeyOf(HttpRequestMessage request)
    {
        var body = request.Content!.ReadFromJsonAsync<CreatePublicShareLinkRequest>().GetAwaiter().GetResult()!;
        return $"{body.ItemType}/{body.ItemId}";
    }

    private static Task<HttpResponseMessage> Json<TBody>(TBody body)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
