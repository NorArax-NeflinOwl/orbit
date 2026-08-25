using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Users;
using Orbit.Core.Users.SaveOwnLocation;
using Xunit;

namespace Orbit.Api.Tests.Users;

/// <summary>
/// Covers the one point Orbit keeps per user: that it validates, that recording again replaces rather
/// than accumulates, and that clearing it leaves nothing behind.
/// </summary>
public sealed class UserLocationTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Recording_a_location_stores_the_point_and_its_address()
    {
        var context = new UserLocationTestContext();

        var saved = await context.RecordAsync(new UserLocation("Warsaw, Poland", 52.2297, 21.0122, Noon));

        Assert.True(saved);
        var location = (await context.ReloadAsync()).Location;
        Assert.Equal("Warsaw, Poland", location!.Address);
        Assert.Equal(52.2297, location.Latitude);
        Assert.Equal(21.0122, location.Longitude);
    }

    [Fact]
    public async Task A_point_with_no_address_is_still_worth_keeping()
    {
        // Reverse geocoding has nothing for open water, and a location is useful without a street name.
        var context = new UserLocationTestContext();

        await context.RecordAsync(new UserLocation(null, 0, 0, Noon));

        Assert.Null((await context.ReloadAsync()).Location!.Address);
    }

    [Fact]
    public async Task Recording_again_replaces_the_previous_point()
    {
        var context = new UserLocationTestContext();
        await context.RecordAsync(new UserLocation("Warsaw, Poland", 52.2297, 21.0122, Noon));

        await context.RecordAsync(new UserLocation("Kraków, Poland", 50.0617, 19.9373, Noon.AddHours(3)));

        // One point per user and no trail: the old one is gone, not archived.
        var location = (await context.ReloadAsync()).Location;
        Assert.Equal("Kraków, Poland", location!.Address);
        Assert.Equal(Noon.AddHours(3), location.RecordedAtUtc);
    }

    [Fact]
    public async Task Clearing_leaves_nothing_behind()
    {
        var context = new UserLocationTestContext();
        await context.RecordAsync(new UserLocation("Warsaw, Poland", 52.2297, 21.0122, Noon));

        await context.RecordAsync(location: null);

        Assert.Null((await context.ReloadAsync()).Location);
    }

    [Theory]
    [InlineData(90.1, 21.0)]
    [InlineData(-90.1, 21.0)]
    [InlineData(52.2, 180.1)]
    [InlineData(52.2, -180.1)]
    public void A_point_off_the_globe_is_refused(double latitude, double longitude)
    {
        // The same rule a calendar event's location follows, and refused the same way - a 400 naming
        // what was wrong rather than a stored point nobody can explain later.
        Assert.Throws<InvalidRequestException>(() => new UserLocation(null, latitude, longitude, Noon));
    }

    [Theory]
    [InlineData(90.0, 180.0)]
    [InlineData(-90.0, -180.0)]
    public void The_edges_of_the_globe_are_accepted(double latitude, double longitude)
    {
        var location = new UserLocation(null, latitude, longitude, Noon);

        Assert.Equal(latitude, location.Latitude);
    }

    [Fact]
    public async Task Recording_for_an_account_that_does_not_exist_saves_nothing()
    {
        var context = new UserLocationTestContext();

        var saved = await context.RecordAsync(new UserLocation(null, 52.2297, 21.0122, Noon), userId: Guid.NewGuid());

        Assert.False(saved);
    }

    private sealed class UserLocationTestContext
    {
        private readonly InMemoryUserRepository _userRepository = new();
        private readonly Guid _userId = Guid.NewGuid();

        public UserLocationTestContext()
        {
            var user = User.FromPersistence(
                _userId, "walker@example.com", "walker", "Walker", "hash", DateTimeOffset.UtcNow, publicKeyBase64: null);
            _userRepository.AddAsync(user, CancellationToken.None).GetAwaiter().GetResult();
        }

        public Task<bool> RecordAsync(UserLocation? location, Guid? userId = null)
            => new SaveOwnLocationCommandHandler(_userRepository)
                .HandleAsync(new SaveOwnLocationCommand(userId ?? _userId, location), CancellationToken.None);

        public async Task<User> ReloadAsync()
            => (await _userRepository.GetByIdAsync(_userId, CancellationToken.None))!;
    }
}
