using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Places;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The places screen, and the place behind it. It reads the local database and never the API, which is
/// what makes it work with no connection - so most of what these hold in place is the round trip: what
/// the server has reaches the phone, what the phone does reaches the server, and what neither can do
/// while offline is refused rather than queued and lost.
/// </summary>
public sealed class PlacesScreenTests
{
    [Fact]
    public async Task What_the_server_keeps_reaches_the_phone()
    {
        using var context = new PlacesContext();
        context.Server.AddPlace("The good bakery");

        var screen = context.OpenList();
        await screen.LoadCommand.ExecuteAsync(null);

        var row = Assert.Single(screen.Places);
        Assert.Equal("The good bakery", row.Name);
        Assert.Equal("Piękna 1, Warszawa", row.Address);
    }

    /// <summary>
    /// A place handed over reaches this phone through the same feed as one the reader kept - the server
    /// answers with both - and the row says which it is, because those are different things.
    /// </summary>
    [Fact]
    public async Task A_place_somebody_handed_over_says_who_it_came_from()
    {
        using var context = new PlacesContext();
        context.Server.AddPlace("Their bakery", isShared: true, sharedBy: "Anna", accessLevel: "ReadOnly");

        var screen = context.OpenList();
        await screen.LoadCommand.ExecuteAsync(null);

        var row = Assert.Single(screen.Places);
        Assert.True(row.IsSharedWithMe);
        Assert.Equal("Anna", row.SharedBy);
        // Getting rid of it is not destroying what somebody else keeps, and the row says so.
        Assert.Equal("Take it off my map", row.DeleteLabel);
    }

    /// <summary>Keeping one is a name here and the rest on its own screen, which is where it opens.</summary>
    [Fact]
    public async Task Keeping_a_place_opens_it_so_the_rest_can_be_filled_in()
    {
        using var context = new PlacesContext();
        var screen = context.OpenList();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.NewPlaceName = "The good bakery";
        await screen.AddPlaceCommand.ExecuteAsync(null);

        Assert.Equal("The good bakery", Assert.Single(screen.Places).Name);
        Assert.Contains("ShowPlace", context.Navigator.Destinations);
    }

    /// <summary>
    /// The one thing a list of places is actually for. Handed to whatever the phone uses for directions
    /// rather than drawn here - see IMapHandoff.
    /// </summary>
    [Fact]
    public async Task Its_point_is_handed_to_the_phones_own_maps()
    {
        using var context = new PlacesContext();
        context.Server.AddPlace("The good bakery", latitude: 51.24, longitude: 22.56);
        var screen = context.OpenList();
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.OpenInMapsCommand.ExecuteAsync(screen.Places[0]);

        var shown = Assert.Single(context.Maps.Shown);
        Assert.Equal(51.24, shown.Latitude);
        Assert.Equal("The good bakery", shown.Label);
    }

    /// <summary>What the reader writes here reaches the server, which is the other half of the round trip.</summary>
    [Fact]
    public async Task What_the_phone_writes_reaches_the_server()
    {
        using var context = new PlacesContext();
        var stored = await context.Places.CreateAsync(
            new PlaceContent("Bakery", "", "Rynek 1", 51.24, 22.56), CancellationToken.None);

        var screen = context.OpenDetail(stored.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);
        screen.Name = "The good bakery";
        await screen.SaveCommand.ExecuteAsync(null);

        Assert.Equal("The good bakery", Assert.Single(context.Server.Places).Name);
    }

    /// <summary>
    /// A place with no point cannot be drawn on a map and cannot be handed to one, so it is not saved -
    /// the same pair the server refuses without.
    /// </summary>
    [Fact]
    public async Task A_place_with_no_point_cannot_be_saved_yet()
    {
        using var context = new PlacesContext();
        var stored = await context.Places.CreateAsync(
            new PlaceContent("Bakery", "", "", 0, 0), CancellationToken.None);

        var screen = context.OpenDetail(stored.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(screen.CanSave);
        Assert.True(screen.NeedsAPoint);
    }

    /// <summary>
    /// Pointing at it on a map is the other way to say where it is. The point is taken whatever the box
    /// says; the words only where there are none, so "the back entrance" survives being pinned.
    /// </summary>
    [Fact]
    public async Task A_pin_gives_it_a_point_and_leaves_words_somebody_typed_alone()
    {
        using var context = new PlacesContext();
        var stored = await context.Places.CreateAsync(
            new PlaceContent("Bakery", "", "The back entrance", 0, 0), CancellationToken.None);
        var screen = context.OpenDetail(stored.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.PickOnMapCommand.ExecuteAsync(null);

        Assert.True(screen.HasAPoint);
        Assert.Equal("The back entrance", screen.Address);
        Assert.True(screen.CanSave);
    }

    /// <summary>
    /// Read-only means read-only. Said here as well as on the server, so the screen does not offer a
    /// Save that would only fail - see SharedItemAccess.
    /// </summary>
    [Fact]
    public async Task A_place_handed_over_to_read_is_not_editable_here()
    {
        using var context = new PlacesContext();
        context.Server.AddPlace("Their bakery", isShared: true, sharedBy: "Anna", accessLevel: "ReadOnly");
        var list = context.OpenList();
        await list.LoadCommand.ExecuteAsync(null);

        var screen = context.OpenDetail(list.Places[0].LocalId);
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(screen.CanEdit);
        Assert.False(screen.CanSave);
        Assert.NotEqual(string.Empty, screen.WhyItIsReadOnly);
    }

    /// <summary>
    /// Offline, a place somebody else can change is read-only: the phone cannot hold the lock that
    /// protects it, so an edit could only be found to be impossible at replay time - see
    /// OfflineEditPolicy, which holds that rule for every kind of thing here.
    /// </summary>
    [Fact]
    public async Task Offline_a_place_somebody_else_can_change_is_left_alone()
    {
        using var context = new PlacesContext();
        context.Server.AddPlace("Our bakery", isSharedWithOthers: true);
        var list = context.OpenList();
        await list.LoadCommand.ExecuteAsync(null);
        context.Network.Becomes(false);

        var screen = context.OpenDetail(list.Places[0].LocalId);
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(screen.CanEdit);
    }

    /// <summary>A place forgotten on the server goes from the phone too - that is what the delta is for.</summary>
    [Fact]
    public async Task A_place_forgotten_elsewhere_goes_from_the_phone()
    {
        using var context = new PlacesContext();
        var onTheServer = context.Server.AddPlace("The good bakery");
        var screen = context.OpenList();
        await screen.LoadCommand.ExecuteAsync(null);
        Assert.Single(screen.Places);

        context.Server.Forget(onTheServer.Id);
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Empty(screen.Places);
    }

    /// <summary>Everything a places test needs: a local store, a fake server, and the two screens.</summary>
    private sealed class PlacesContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly Translations _translations = new(new InMemoryLanguageStore());
        private readonly SyncGate _gate = new();

        public PlacesContext()
        {
            Server = new FakePlacesServer(TimeProvider.System);
            Places = new LocalPlaceRepository(_localStore, TimeProvider.System, Network);
        }

        public FakePlacesServer Server { get; }

        public LocalPlaceRepository Places { get; }

        public FixedNetworkStatus Network { get; } = FixedNetworkStatus.Online;

        public RecordingScreenNavigator Navigator { get; } = new();

        public RecordingMapHandoff Maps { get; } = new();

        public FixedPlacePicker Picker { get; } = new();

        private PlaceSynchronizer Synchronizer => new(
            _localStore, new PlacesClient(Server.ToHttpClient()), TimeProvider.System, _gate,
            NullLogger<PlaceSynchronizer>.Instance);

        public PlacesViewModel OpenList() => new(
            Places, Synchronizer, Network, _translations, new SyncState(Network, TimeProvider.System), Navigator, Maps,
            TimeProvider.System, new InMemoryListArrangementStore());

        public PlaceDetailViewModel OpenDetail(Guid localId)
        {
            var screen = new PlaceDetailViewModel(
                Places, Synchronizer, Picker, Maps, _translations, Network, Navigator,
                // Sharing is not what these are about; the panel is here because the screen holds one.
                ShareTestPanel.For(_localStore, new ChatRepository(_localStore, TimeProvider.System)));
            screen.Open(localId);
            return screen;
        }

        public void Dispose() => _localStore.Dispose();
    }
}
