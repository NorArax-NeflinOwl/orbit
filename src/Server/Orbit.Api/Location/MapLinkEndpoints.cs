using Orbit.Contracts.Location;
using Orbit.Core.Abstractions;
using Orbit.Core.Location.ResolveMapLink;

namespace Orbit.Api.Location;

/// <summary>
/// Where a link to somebody else's map points, for the one kind a client cannot read for itself: a
/// shortened one carries an identifier, and only the service that made it knows what is behind it - see
/// ShortenedLinkFollower, which is the only thing here that reaches outside.
/// </summary>
public static class MapLinkEndpoints
{
    public static void MapMapLinkEndpoints(this WebApplication app)
    {
        // Signed in, like everything else: this asks a question of somebody else's service on the
        // caller's behalf, and an open endpoint that did that would be one anybody could aim.
        var links = app.MapGroup("/api/location/map-link").RequireAuthorization();

        links.MapGet("/", async (string url, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var link = await dispatcher.SendAsync(new ResolveMapLinkQuery(url), cancellationToken);
            // 204 for a link Orbit cannot place, which is an answer rather than a failure: the reader is
            // shown the link they were given and told so.
            return link is null
                ? Results.NoContent()
                : Results.Ok(new ResolvedMapLinkDto(
                    link.Latitude, link.Longitude, link.Search, link.Label, link.Url));
        });
    }
}
