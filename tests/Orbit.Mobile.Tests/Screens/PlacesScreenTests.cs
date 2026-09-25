using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Location;
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
    /// A place the browser made from a Location entry of a task list says so on this phone too. The
    /// server has held the link since 2026-09-11 (TaskEntryPlaces, Place.SourceTaskItemId) and the
    /// phone threw it away on every sync, so a place made from an entry was indistinguishable here from
    /// one somebody kept by hand.
    /// </summary>
    [Fact]
    public async Task A_place_made_from_a_task_entry_says_which_entry_on_this_phone_too()
    {
        using var context = new PlacesContext();
        var entryId = Guid.NewGuid();
        context.Server.AddPlace("The chemist", sourceTaskItemId: entryId);
        context.Server.AddPlace("The good bakery");

        var screen = context.OpenList();
        await screen.LoadCommand.ExecuteAsync(null);

        var stored = await context.Places.GetAllAsync();
        Assert.Equal(entryId, Assert.Single(stored, place => place.Name == "The chemist").SourceTaskItemId);
        // And one kept by hand still answers to nothing, which is what tells the two apart.
        Assert.Null(Assert.Single(stored, place => place.Name == "The good bakery").SourceTaskItemId);
    }

    /// <summary>
    /// Editing such a place here must not cut it loose from its entry. The phone sends no
    /// SourceTaskItemId at all, and null on a save means "leave it alone" - the one field in
    /// SavePlaceRequest where it does, written that way for exactly this.
    /// </summary>
    [Fact]
    public async Task And_changing_it_here_leaves_it_answering_to_that_entry()
    {
        using var context = new PlacesContext();
        var entryId = Guid.NewGuid();
        var onTheServer = context.Server.AddPlace("The chemist", sourceTaskItemId: entryId);
        var list = context.OpenList();
        await list.LoadCommand.ExecuteAsync(null);
        var stored = Assert.Single(await context.Places.GetAllAsync());

        var detail = context.OpenDetail(stored.LocalId);
        detail.Name = "The chemist on the corner";
        await detail.SaveCommand.ExecuteAsync(null);
        await list.LoadCommand.ExecuteAsync(null);

        var afterwards = Assert.Single(context.Server.Places, place => place.Id == onTheServer.Id);
        Assert.Equal(entryId, afterwards.SourceTaskItemId);
        Assert.Equal(entryId, Assert.Single(await context.Places.GetAllAsync()).SourceTaskItemId);
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

    /// <summary>
    /// What the reader writes here reaches the server, which is the other half of the round trip - and
    /// for a place it reaches it <b>sealed</b>, because a place is private unless its owner said
    /// otherwise. What the server ends up holding is ciphertext and three empty fields.
    /// </summary>
    [Fact]
    public async Task What_the_phone_writes_reaches_the_server_sealed()
    {
        using var context = new PlacesContext();
        var stored = await context.Places.CreateAsync(
            new PlaceContent("Bakery", "", "Rynek 1", 51.24, 22.56), CancellationToken.None);

        var screen = context.OpenDetail(stored.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);
        screen.Name = "The good bakery";
        await screen.SaveCommand.ExecuteAsync(null);

        var onTheServer = Assert.Single(context.Server.Places);
        Assert.True(onTheServer.IsPrivate);
        Assert.NotNull(onTheServer.EncryptedContent);
        // Not the name, not the address, and not the point: a place whose coordinates travelled in the
        // clear would be sealed in name only.
        Assert.Equal(string.Empty, onTheServer.Name);
        Assert.Equal(string.Empty, onTheServer.Where.Address);
        Assert.Equal(0, onTheServer.Where.Latitude);
    }

    /// <summary>
    /// And it comes back readable on this phone, which is the point of sealing rather than of hiding:
    /// the words are here, and only here.
    /// </summary>
    [Fact]
    public async Task A_sealed_place_is_still_readable_on_the_phone_that_sealed_it()
    {
        using var context = new PlacesContext();
        await context.Places.CreateAsync(
            new PlaceContent("The good bakery", "Sourdough", "Rynek 1", 51.24, 22.56), CancellationToken.None);

        var screen = context.OpenList();
        await screen.LoadCommand.ExecuteAsync(null);

        var row = Assert.Single(screen.Places);
        Assert.Equal("The good bakery", row.Name);
        Assert.Equal("Rynek 1", row.Address);
    }

    /// <summary>
    /// A place is sealed unless somebody says otherwise, which is the opposite default from every other
    /// kind of thing here - see Orbit.Core.Places.Place.IsPrivate. Left open, it is stored open.
    /// </summary>
    [Fact]
    public async Task A_place_is_sealed_unless_it_is_told_not_to_be()
    {
        using var context = new PlacesContext();

        var sealedByDefault = await context.Places.CreateAsync(
            new PlaceContent("The good bakery", "", "Rynek 1", 51.24, 22.56), CancellationToken.None);
        var left0pen = await context.Places.CreateAsync(
            new PlaceContent("The open bakery", "", "Rynek 2", 51.25, 22.57, IsPrivate: false),
            CancellationToken.None);

        Assert.True(sealedByDefault.IsPrivate);
        Assert.Equal(string.Empty, sealedByDefault.Name);
        Assert.NotNull(sealedByDefault.EncryptedContent);
        Assert.False(left0pen.IsPrivate);
        Assert.Equal("The open bakery", left0pen.Name);
        Assert.Null(left0pen.EncryptedContent);
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
    /// An address typed into the box is looked up when the place is saved, the way the event's form
    /// looks up the place typed into it. Until 2026-09-20 the only way to give a place a point was the
    /// map: Save stayed greyed however much was typed here, and nothing said why.
    /// </summary>
    [Fact]
    public async Task An_address_typed_in_is_looked_up_when_the_place_is_saved()
    {
        using var context = new PlacesContext
        {
            AddressLookup = StubHttpMessageHandler.RespondingWith(new[]
            {
                new { lat = "51.2465", lon = "22.5684", display_name = "Rynek 1, Lublin" }
            })
        };
        var stored = await context.Places.CreateAsync(
            new PlaceContent("Bakery", "", "", 0, 0), CancellationToken.None);
        var screen = context.OpenDetail(stored.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);

        screen.Address = "Rynek 1, Lublin";
        Assert.True(screen.CanSave);
        await screen.SaveCommand.ExecuteAsync(null);

        var kept = Assert.Single(await context.Places.GetAllAsync(CancellationToken.None));
        Assert.Equal(51.2465, kept.Latitude, 4);
        Assert.Equal(22.5684, kept.Longitude, 4);
    }

    /// <summary>And an address nothing can be found for is said out loud rather than refused in silence.</summary>
    [Fact]
    public async Task An_address_that_cannot_be_found_says_so()
    {
        using var context = new PlacesContext();
        var stored = await context.Places.CreateAsync(
            new PlaceContent("Bakery", "", "", 0, 0), CancellationToken.None);
        var screen = context.OpenDetail(stored.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);

        screen.Address = "nowhere at all";
        await screen.SaveCommand.ExecuteAsync(null);

        Assert.True(screen.HasMessage);
        Assert.Equal(0, Assert.Single(await context.Places.GetAllAsync(CancellationToken.None)).Latitude);
    }

    /// <summary>
    /// And the place somebody is standing in can be kept without finding it on a map first - the same
    /// press the map's own screen offers, asked for on 2026-09-20.
    /// </summary>
    [Fact]
    public async Task My_location_gives_the_place_its_point()
    {
        using var context = new PlacesContext();
        var stored = await context.Places.CreateAsync(
            new PlaceContent("Bakery", "", "", 0, 0), CancellationToken.None);
        var screen = context.OpenDetail(stored.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.UseMyLocationCommand.ExecuteAsync(null);

        Assert.True(screen.HasAPoint);
        // The address comes with it while the box is empty - somebody's own words would be worth more.
        Assert.Equal("Marszałkowska, Warszawa, Poland", screen.Address);
        Assert.True(screen.CanSave);
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
    /// A pin fills in where the place is and never what it is called: the name is the reader's to give,
    /// and Save waits for one. The pin's address used to be written in as the name, so a place was kept
    /// under the street already shown beside it.
    /// </summary>
    [Fact]
    public async Task A_pin_leaves_the_name_for_the_reader_to_give()
    {
        using var context = new PlacesContext();
        var stored = await context.Places.CreateAsync(new PlaceContent("Bakery", "", "", 0, 0), CancellationToken.None);
        var screen = context.OpenDetail(stored.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);
        screen.Name = string.Empty;

        await screen.PickOnMapCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, screen.Name);
        Assert.False(screen.CanSave);
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

    /// <summary>
    /// Putting a place away takes it off the list and puts it in the archive, which is the same screen
    /// showing the other half of what it holds - the browser draws that division as a page of its own
    /// (MapArchive). Asked for on 2026-09-19.
    /// </summary>
    [Fact]
    public async Task A_place_put_away_leaves_the_list_and_is_in_the_archive()
    {
        using var context = new PlacesContext();
        context.Server.AddPlace("The good bakery");
        var screen = context.OpenList();
        await screen.LoadCommand.ExecuteAsync(null);
        // From the place's own screen, which is where this phone keeps what can be done to a thing -
        // its list rows carry no such press, on purpose (see NotesPage, which archives the same way).
        var place = context.OpenDetail(screen.Places[0].LocalId);
        await place.LoadCommand.ExecuteAsync(null);

        await place.ArchiveCommand.ExecuteAsync(true);
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Empty(screen.Places);
        await screen.ShowTheArchiveCommand.ExecuteAsync(true);
        Assert.Equal("The good bakery", Assert.Single(screen.Places).Name);
        Assert.Equal("Archived places", screen.Heading);
    }

    /// <summary>And the server is told, on its own endpoint - see ArchivePlaceCommand.</summary>
    [Fact]
    public async Task Putting_a_place_away_reaches_the_server()
    {
        using var context = new PlacesContext();
        var onTheServer = context.Server.AddPlace("The good bakery");
        var list = context.OpenList();
        await list.LoadCommand.ExecuteAsync(null);
        var screen = context.OpenDetail(list.Places[0].LocalId);
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.ArchiveCommand.ExecuteAsync(true);

        Assert.Contains($"PUT /api/places/{onTheServer.Id}/archived", context.Server.ReceivedRequests);
        Assert.True(context.Server.Places.Single().IsArchived);
    }

    /// <summary>And back again, from the same menu, which then says "Put back".</summary>
    [Fact]
    public async Task A_place_is_brought_back_from_the_archive()
    {
        using var context = new PlacesContext();
        context.Server.AddPlace("Last year's flat", isArchived: true);
        var list = context.OpenList();
        await list.LoadCommand.ExecuteAsync(null);
        await list.ShowTheArchiveCommand.ExecuteAsync(true);
        var screen = context.OpenDetail(list.Places[0].LocalId);
        await screen.LoadCommand.ExecuteAsync(null);
        Assert.True(screen.IsArchived);

        await screen.ArchiveCommand.ExecuteAsync(false);
        await list.LoadCommand.ExecuteAsync(null);

        Assert.Empty(list.Places);
        await list.ShowTheArchiveCommand.ExecuteAsync(false);
        Assert.Equal("Last year's flat", Assert.Single(list.Places).Name);
    }

    /// <summary>
    /// A place put away in a browser is put away here too, the first time this phone hears about it -
    /// which is what the flag on the way down is for (see PlaceSynchronizer.CopyInto).
    /// </summary>
    [Fact]
    public async Task A_place_put_away_elsewhere_leaves_this_phones_list()
    {
        using var context = new PlacesContext();
        context.Server.AddPlace("The good bakery", isArchived: true);
        var screen = context.OpenList();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Empty(screen.Places);
    }

    /// <summary>
    /// The place's own screen says which it is, so its menu can offer Archive or Put back - and, since
    /// deleting a place is offered in the archive and nowhere else, whether to offer Delete at all.
    /// </summary>
    [Fact]
    public async Task The_places_own_screen_says_whether_it_has_been_put_away()
    {
        using var context = new PlacesContext();
        context.Server.AddPlace("The good bakery");
        var list = context.OpenList();
        await list.LoadCommand.ExecuteAsync(null);
        var screen = context.OpenDetail(list.Places[0].LocalId);
        await screen.LoadCommand.ExecuteAsync(null);
        Assert.False(screen.IsArchived);

        await screen.ArchiveCommand.ExecuteAsync(true);

        Assert.True(screen.IsArchived);
        Assert.Contains("archive", screen.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A place kept while this phone was offline is created and put away in the same pass once it is
    /// back - a create has no room for the flag, so the archiving follows it. See
    /// PlaceSynchronizer.SendCreateAsync.
    /// </summary>
    [Fact]
    public async Task A_place_put_away_before_the_server_ever_saw_it_arrives_put_away()
    {
        using var context = new PlacesContext();
        context.Server.IsUnreachable = true;
        var screen = context.OpenList();
        await screen.LoadCommand.ExecuteAsync(null);
        screen.NewPlaceName = "The good bakery";
        await screen.AddPlaceCommand.ExecuteAsync(null);
        var place = context.OpenDetail(screen.Places[0].LocalId);
        await place.LoadCommand.ExecuteAsync(null);
        await place.ArchiveCommand.ExecuteAsync(true);

        context.Server.IsUnreachable = false;
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(context.Server.Places.Single().IsArchived);
    }

    /// <summary>
    /// A sealed place is offered to nobody: the server holds no readable copy to hand over, which is
    /// what makes it sealed, and it refuses the share outright (SharePlaceCommandHandler). This screen
    /// was the one that offered it anyway - and since a place is sealed unless its owner says
    /// otherwise, the ordinary case was the refused one: pressing Share and choosing somebody answered
    /// "Couldn't share that." Reported 2026-09-20 as "sharing doesn't work".
    /// </summary>
    [Fact]
    public async Task A_sealed_place_offers_no_sharing_and_says_why()
    {
        using var context = new PlacesContext();
        var kept = await context.Places.CreateAsync(
            new PlaceContent("The good bakery", "", "Rynek 1", 51.24, 22.56), CancellationToken.None);
        // Saved once, so the server knows it: a place it has never seen is offered to nobody either,
        // and this is about the seal rather than about that.
        var first = context.OpenDetail(kept.LocalId);
        await first.LoadCommand.ExecuteAsync(null);
        await first.SaveCommand.ExecuteAsync(null);

        var screen = context.OpenDetail(kept.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.IsSealed);
        Assert.False(screen.Share.CanShare);
        Assert.True(screen.HasWhyItCannotBeShared);
    }

    /// <summary>And one whose seal has been taken off can be handed to somebody.</summary>
    [Fact]
    public async Task Taking_the_seal_off_makes_a_place_shareable()
    {
        using var context = new PlacesContext();
        var kept = await context.Places.CreateAsync(
            new PlaceContent("The good bakery", "", "Rynek 1", 51.24, 22.56), CancellationToken.None);
        var screen = context.OpenDetail(kept.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);

        screen.IsSealed = false;
        await screen.SaveCommand.ExecuteAsync(null);
        var reopened = context.OpenDetail(kept.LocalId);
        await reopened.LoadCommand.ExecuteAsync(null);

        Assert.False(reopened.IsSealed);
        Assert.True(reopened.Share.CanShare);
        Assert.False(reopened.HasWhyItCannotBeShared);
        // And the words are readable again on the server's side of it - sealing empties them.
        Assert.Equal("The good bakery", Assert.Single(context.Server.Places).Name);
    }

    /// <summary>
    /// A save keeps the seal the place has rather than the one the content record defaults to. It
    /// defaults to sealed, so editing a place whose owner had opened it on the browser silently sealed
    /// it again - and sealing empties the name, the address and the point.
    /// </summary>
    [Fact]
    public async Task Saving_an_open_place_leaves_it_open()
    {
        using var context = new PlacesContext();
        var kept = await context.Places.CreateAsync(
            new PlaceContent("The open bakery", "", "Rynek 2", 51.25, 22.57, IsPrivate: false),
            CancellationToken.None);
        var screen = context.OpenDetail(kept.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);

        screen.Description = "Sourdough";
        await screen.SaveCommand.ExecuteAsync(null);

        var stored = Assert.Single(await context.Places.GetAllAsync(CancellationToken.None));
        Assert.False(stored.IsPrivate);
        Assert.Equal("The open bakery", stored.Name);
    }

    /// <summary>
    /// And it keeps the lists the place belongs to. Nothing on this screen shows them, and a save wrote
    /// the whole place - so editing one on the phone unlinked it from every list the browser had put it
    /// on. See LocalPlace.TaskListIds.
    /// </summary>
    [Fact]
    public async Task Saving_a_place_leaves_the_lists_it_belongs_to_alone()
    {
        using var context = new PlacesContext();
        var onAList = Guid.NewGuid();
        var kept = await context.Places.CreateAsync(
            new PlaceContent(
                "The good bakery", "", "Rynek 1", 51.24, 22.56, TaskListIds: [onAList], IsPrivate: false),
            CancellationToken.None);
        var screen = context.OpenDetail(kept.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);

        screen.Description = "Sourdough";
        await screen.SaveCommand.ExecuteAsync(null);

        var stored = Assert.Single(await context.Places.GetAllAsync(CancellationToken.None));
        Assert.Equal([onAList], stored.TaskListIds);
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
            // With a key, because a place is sealed unless its owner says otherwise - a store that
            // cannot unlock one would leave every place in these tests unreadable.
            Places = new LocalPlaceRepository(_localStore, TimeProvider.System, Network, PrivateContent.WithAKey());
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
            Places, Synchronizer, Network, _translations, new SyncState(Reachability.Over(Network), TimeProvider.System), Navigator, Maps,
            TimeProvider.System, new InMemoryListArrangementStore());

        /// <summary>Where the phone says it is, for "Use my location" - see FixedDeviceLocation.</summary>
        public FixedDeviceLocation Here { get; } = new();

        /// <summary>
        /// What an address typed into the screen comes back as. Nothing by default: most of these
        /// tests pin the place rather than type it, and a lookup nobody arranged should find nothing.
        /// </summary>
        public StubHttpMessageHandler AddressLookup { get; set; } = StubHttpMessageHandler.RespondingWith(Array.Empty<object>());

        public PlaceDetailViewModel OpenDetail(Guid localId)
        {
            var screen = new PlaceDetailViewModel(
                Places, Synchronizer, Picker, Maps, _translations, Network, Navigator,
                // Sharing is not what these are about; the panel is here because the screen holds one.
                ShareTestPanel.For(_localStore, new ChatRepository(_localStore, TimeProvider.System)),
                Here, new PlaceSearch(AddressLookup.ToHttpClient()));
            screen.Open(localId);
            return screen;
        }

        public void Dispose() => _localStore.Dispose();
    }
}
