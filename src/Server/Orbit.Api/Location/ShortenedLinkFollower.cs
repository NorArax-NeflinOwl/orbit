using Orbit.Core.Location;

namespace Orbit.Api.Location;

/// <summary>
/// Follows a shortened map link one hop, and no further - see IShortenedLinkFollower for why this
/// cannot be done in a browser at all.
///
/// <b>It will only ever ask the shorteners themselves.</b> The address to follow arrives from a caller,
/// and an endpoint that fetched whatever it was given would be a way of asking Orbit's own machine to
/// reach things nobody outside it can - the container's neighbours, the metadata service. So the host
/// is checked against MapLinks' own short list before anything is sent, redirects are not followed
/// automatically (the one hop is read out of the header), no credentials go with the request, and the
/// answer's body is never read: what is wanted is one header.
/// </summary>
public sealed class ShortenedLinkFollower : IShortenedLinkFollower
{
    /// <summary>
    /// Short on purpose. A reader is waiting on this with a map in front of them, and a shortener that
    /// is slow to answer is one Orbit says it could not place the link with.
    /// </summary>
    private static readonly TimeSpan HowLongToWait = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly ILogger<ShortenedLinkFollower> _logger;

    public ShortenedLinkFollower(HttpClient httpClient, ILogger<ShortenedLinkFollower> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> FollowAsync(string url, CancellationToken cancellationToken)
    {
        if (!MapLinks.IsAShortenedLink(url))
        {
            // Nothing to follow, and nothing this is allowed to ask for - see the note above.
            return null;
        }

        using var asking = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        asking.CancelAfter(HowLongToWait);
        try
        {
            // The headers and not the body: the answer is a redirect, and its page - a consent screen,
            // an app banner - is nothing Orbit reads.
            using var answer = await _httpClient.GetAsync(
                url, HttpCompletionOption.ResponseHeadersRead, asking.Token);
            var location = answer.Headers.Location;
            return location is null
                ? null
                // Relative, where a service answers with a path: read against the link it came from,
                // which is the only address it can mean.
                : location.IsAbsoluteUri ? location.ToString() : new Uri(new Uri(url), location).ToString();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // An ordinary answer rather than a failure - the reader is shown the link they were given
            // and told Orbit could not place it.
            _logger.LogInformation(exception, "Could not follow a shortened map link");
            return null;
        }
    }
}
