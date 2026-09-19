namespace Orbit.Core.Location;

/// <summary>
/// Follows a shortened map link to the address it stands for. An interface because the following is an
/// HTTP request to somebody else's service, which is the API's business and not the domain's - the rule
/// every other outside service here follows.
///
/// A browser cannot do this itself: the shortener answers a redirect without the headers that would let
/// a page read it, so the one thing standing between a pasted <c>maps.app.goo.gl</c> link and the place
/// it names is a request made from somewhere that is not a browser. See MapLinks, which reads the
/// address once it is known.
/// </summary>
public interface IShortenedLinkFollower
{
    /// <summary>
    /// Where the shortener says this link goes, or null where it will not say - the service is down,
    /// the link has expired, the answer is not an address at all. Null is an ordinary answer here and
    /// not a failure: the reader is shown the link they were given and told Orbit could not place it.
    /// </summary>
    Task<string?> FollowAsync(string url, CancellationToken cancellationToken);
}
