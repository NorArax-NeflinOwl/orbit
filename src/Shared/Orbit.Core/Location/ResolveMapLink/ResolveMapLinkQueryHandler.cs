using Orbit.Core.Abstractions;

namespace Orbit.Core.Location.ResolveMapLink;

/// <summary>
/// Reads the link, and where reading it is not enough, follows it once and reads what comes back.
///
/// Only a shortened link is followed. Everything else is answered from the link itself, which the
/// client could have done - the endpoint takes both so a client has one question to ask rather than a
/// rule of its own about which links are worth asking about.
///
/// One hop, and the answer has to be a map link in its own right: a shortener that answered with
/// somewhere else entirely is a shortener saying nothing about a map, and following further would be
/// Orbit walking a chain somebody else controls.
/// </summary>
public sealed class ResolveMapLinkQueryHandler : IRequestHandler<ResolveMapLinkQuery, MapLink?>
{
    private readonly IShortenedLinkFollower _follower;

    public ResolveMapLinkQueryHandler(IShortenedLinkFollower follower) => _follower = follower;

    public async Task<MapLink?> HandleAsync(ResolveMapLinkQuery request, CancellationToken cancellationToken)
    {
        if (MapLinks.Read(request.Url) is not { } link)
        {
            return null;
        }

        if (!MapLinks.IsAShortenedLink(request.Url))
        {
            return link;
        }

        var behindIt = MapLinks.Read(await _follower.FollowAsync(request.Url, cancellationToken));
        return behindIt is null || (!behindIt.HasAPoint && behindIt.Search is null)
            ? null
            // The place it turned out to be, still carrying the link somebody actually wrote: that is
            // what "open the original" has to open, and it is not the address the shortener gave back.
            : behindIt with { Url = request.Url };
    }
}
