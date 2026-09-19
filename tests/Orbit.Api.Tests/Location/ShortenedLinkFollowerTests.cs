using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Api.Location;
using Orbit.Api.Tests.TestDoubles;
using Xunit;

namespace Orbit.Api.Tests.Location;

/// <summary>
/// The one thing in Orbit that fetches an address a caller handed it - see ShortenedLinkFollower, which
/// is why the host is checked before anything is sent. What this holds in place is that rule: an
/// endpoint that fetched whatever it was given would be a way of asking Orbit's own machine to reach
/// things nobody outside it can.
/// </summary>
public sealed class ShortenedLinkFollowerTests
{
    private readonly List<string> _asked = [];

    private ShortenedLinkFollower Following(string? answersWith)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            _asked.Add(request.RequestUri!.ToString());
            var answer = new HttpResponseMessage(HttpStatusCode.Redirect);
            if (answersWith is not null)
            {
                answer.Headers.Location = new Uri(answersWith, UriKind.RelativeOrAbsolute);
            }

            return Task.FromResult(answer);
        }));

        return new ShortenedLinkFollower(httpClient, NullLogger<ShortenedLinkFollower>.Instance);
    }

    [Fact]
    public async Task A_shortener_is_followed_one_hop()
    {
        var follower = Following("https://www.google.com/maps/@52.2297,21.0122,17z");

        var followed = await follower.FollowAsync("https://maps.app.goo.gl/AbCdEf123", CancellationToken.None);

        Assert.Equal("https://www.google.com/maps/@52.2297,21.0122,17z", followed);
    }

    /// <summary>
    /// And nothing else is ever asked for. Not another host, not a map service that is not a shortener,
    /// and not something inside the network Orbit is running in.
    /// </summary>
    [Theory]
    [InlineData("https://example.com/anything")]
    [InlineData("https://www.google.com/maps/@52.2297,21.0122,17z")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://localhost:8080/api/users")]
    [InlineData("file:///etc/passwd")]
    public async Task Nothing_but_a_shortener_is_ever_asked(string url)
    {
        var follower = Following("https://www.google.com/maps/@52.2297,21.0122,17z");

        Assert.Null(await follower.FollowAsync(url, CancellationToken.None));
        Assert.Empty(_asked);
    }

    /// <summary>An answer with no address in it is nothing to place - see the handler, which says so.</summary>
    [Fact]
    public async Task A_shortener_that_answers_with_no_address_says_nothing()
        => Assert.Null(await Following(null).FollowAsync(
            "https://maps.app.goo.gl/AbCdEf123", CancellationToken.None));

    /// <summary>
    /// A relative answer is read against the link it came from, which is the only address it can mean.
    /// </summary>
    [Fact]
    public async Task A_relative_answer_is_read_against_the_link_it_came_from()
    {
        var followed = await Following("/maps/@52.2297,21.0122,17z")
            .FollowAsync("https://maps.app.goo.gl/AbCdEf123", CancellationToken.None);

        Assert.Equal("https://maps.app.goo.gl/maps/@52.2297,21.0122,17z", followed);
    }
}
