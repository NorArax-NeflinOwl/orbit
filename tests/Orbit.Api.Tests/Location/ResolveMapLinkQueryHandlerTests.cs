using Orbit.Core.Location;
using Orbit.Core.Location.ResolveMapLink;
using Xunit;

namespace Orbit.Api.Tests.Location;

/// <summary>
/// Where a shortened map link points - the one kind a browser cannot read for itself, because the
/// shortener answers a redirect without the headers that would let a page read it. See MapLinks for
/// every other kind, which is read without asking anybody.
///
/// The following itself is stood in for here: what it does against the real service is not something a
/// test can hold in place, and what this covers is what Orbit does with the answer.
/// </summary>
public sealed class ResolveMapLinkQueryHandlerTests
{
    /// <summary>A shortener that answers with whatever the test says, and remembers what it was asked.</summary>
    private sealed class AShortener : IShortenedLinkFollower
    {
        private readonly string? _answer;

        public AShortener(string? answer) => _answer = answer;

        public string? WasAskedAbout { get; private set; }

        public Task<string?> FollowAsync(string url, CancellationToken cancellationToken)
        {
            WasAskedAbout = url;
            return Task.FromResult(_answer);
        }
    }

    private static Task<MapLink?> ResolveAsync(string url, IShortenedLinkFollower follower)
        => new ResolveMapLinkQueryHandler(follower).HandleAsync(new ResolveMapLinkQuery(url), CancellationToken.None);

    [Fact]
    public async Task A_shortened_link_is_followed_and_read()
    {
        var shortener = new AShortener("https://www.google.com/maps/place/Pa%C5%82ac/@52.2297,21.0122,17z");

        var link = await ResolveAsync("https://maps.app.goo.gl/AbCdEf123", shortener);

        Assert.Equal("https://maps.app.goo.gl/AbCdEf123", shortener.WasAskedAbout);
        Assert.Equal(52.2297, link!.Latitude!.Value, 4);
    }

    /// <summary>
    /// And it still carries the link somebody actually wrote. "Open the original" has to open what was
    /// pasted into the message, not the address a shortener happened to answer with.
    /// </summary>
    [Fact]
    public async Task The_answer_carries_the_link_that_was_written()
    {
        var link = await ResolveAsync(
            "https://maps.app.goo.gl/AbCdEf123",
            new AShortener("https://www.google.com/maps/@52.2297,21.0122,17z"));

        Assert.Equal("https://maps.app.goo.gl/AbCdEf123", link!.Url);
    }

    /// <summary>
    /// A shortener that will not say leaves Orbit with nothing to place - an ordinary answer rather
    /// than a failure, and the page says so and offers the link.
    /// </summary>
    [Fact]
    public async Task A_shortener_that_says_nothing_places_nothing()
        => Assert.Null(await ResolveAsync("https://maps.app.goo.gl/AbCdEf123", new AShortener(null)));

    /// <summary>
    /// And one that answers with somewhere that is not a map at all places nothing either: following
    /// further would be Orbit walking a chain somebody else controls.
    /// </summary>
    [Fact]
    public async Task A_shortener_that_answers_with_something_else_places_nothing()
        => Assert.Null(await ResolveAsync(
            "https://maps.app.goo.gl/AbCdEf123", new AShortener("https://example.com/sign-in")));

    /// <summary>
    /// A link that says where it points is read without anybody being asked - the endpoint takes both
    /// kinds so a client has one question to ask rather than a rule of its own about which is which.
    /// </summary>
    [Fact]
    public async Task A_link_that_says_where_it_points_is_read_without_asking()
    {
        var shortener = new AShortener("https://example.com/never-asked");

        var link = await ResolveAsync("https://www.google.com/maps/@52.2297,21.0122,17z", shortener);

        Assert.Null(shortener.WasAskedAbout);
        Assert.Equal(52.2297, link!.Latitude!.Value, 4);
    }

    [Fact]
    public async Task A_link_that_is_not_a_map_is_not_a_place()
        => Assert.Null(await ResolveAsync("https://example.com/where-we-are", new AShortener(null)));
}
