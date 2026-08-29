using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Location;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// One entry on its own: what it is, when it is, and where. This is what a deadline with a place opens
/// as from the calendar - a checklist answers "where is it?" with a row of text and a tick box, which
/// is no answer for somewhere you have to get to.
///
/// What is worth guarding is where the place comes from. An entry tied to an event takes it from the
/// event, which is where the coordinates are kept; an entry with an address of its own has only words,
/// and those have to be looked up.
/// </summary>
public sealed class TaskItemSummaryScreenTests
{
    [Fact]
    public async Task It_says_what_the_entry_is_and_when_it_is()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel", due: new DateTime(2026, 8, 20, 17, 0, 0));

        var screen = await context.OpenAsync(opened);

        Assert.Equal("Collect the parcel", screen.Description);
        Assert.Equal("Errands", screen.TaskListTitle);
        Assert.Contains("2026", screen.When);
        Assert.False(screen.IsCompleted);
    }

    /// <summary>An entry can lose its date and still be looked at, which is not the same as having none said.</summary>
    [Fact]
    public async Task An_entry_with_no_date_says_so()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel", due: null);

        var screen = await context.OpenAsync(opened);

        Assert.NotEmpty(screen.When);
    }

    [Fact]
    public async Task An_address_of_its_own_is_looked_up_and_pinned()
    {
        using var context = new ScreenContext();
        context.Places = new[] { new { lat = "54.3520", lon = "18.6466", display_name = "Długa 4, Gdańsk" } };
        var opened = await context.AddEntryAsync("Collect the parcel", at: "Długa 4, Gdańsk");

        var screen = await context.OpenAsync(opened);

        Assert.Equal("Długa 4, Gdańsk", screen.Where);
        Assert.NotNull(screen.Pin);
        Assert.Equal(54.3520, screen.Pin.Latitude, precision: 4);
        Assert.True(screen.HasPin);
        Assert.False(screen.IsPlaceUnknown);
    }

    /// <summary>
    /// An address nobody can find stays as the words somebody typed rather than becoming a pin in the
    /// wrong country - the same line Orbit.Web draws.
    /// </summary>
    [Fact]
    public async Task An_address_that_cannot_be_found_keeps_the_words_and_says_so()
    {
        using var context = new ScreenContext();
        context.Places = Array.Empty<object>();
        var opened = await context.AddEntryAsync("Collect the parcel", at: "Nowhere at all");

        var screen = await context.OpenAsync(opened);

        Assert.Equal("Nowhere at all", screen.Where);
        Assert.Null(screen.Pin);
        Assert.True(screen.IsPlaceUnknown);
    }

    /// <summary>
    /// Tied to an event, the event holds the place - one address rather than two that can disagree. No
    /// lookup happens at all, which is the point: the coordinates are already known.
    /// </summary>
    [Fact]
    public async Task An_entry_tied_to_an_event_takes_the_place_from_the_event()
    {
        using var context = new ScreenContext();
        var eventId = await context.AddEventAsync("Dentist", "Wały Piastowskie 1, Gdańsk", 54.3540, 18.6560);
        var opened = await context.AddEntryAsync("Dentist", tiedTo: eventId);

        var screen = await context.OpenAsync(opened);

        Assert.Equal("Wały Piastowskie 1, Gdańsk", screen.Where);
        Assert.NotNull(screen.Pin);
        Assert.Equal(54.3540, screen.Pin.Latitude, precision: 4);
        Assert.Equal(0, context.LookupCount);
    }

    /// <summary>
    /// An event this phone has not got leaves the entry's own words standing rather than the screen
    /// empty - a tie is to something that may not have arrived yet.
    /// </summary>
    [Fact]
    public async Task An_entry_tied_to_an_event_this_phone_has_not_got_falls_back_to_its_own_words()
    {
        using var context = new ScreenContext();
        context.Places = Array.Empty<object>();
        var opened = await context.AddEntryAsync("Dentist", tiedTo: Guid.NewGuid(), at: "Wały Piastowskie 1");

        var screen = await context.OpenAsync(opened);

        Assert.Equal("Wały Piastowskie 1", screen.Where);
    }

    [Fact]
    public async Task An_entry_with_nowhere_at_all_says_that_rather_than_nothing()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel");

        var screen = await context.OpenAsync(opened);

        Assert.NotEmpty(screen.Where);
        Assert.Null(screen.Pin);
        // Nothing was written down, so nothing failed to be found - there is nothing to apologise for.
        Assert.False(screen.IsPlaceUnknown);
    }

    /// <summary>
    /// Crossed off and saved away on another device, or the whole list deleted: there is nothing to
    /// show, and a screen that stayed blank would look broken.
    /// </summary>
    [Fact]
    public async Task An_entry_that_is_gone_sends_the_reader_back_to_the_calendar()
    {
        using var context = new ScreenContext();

        var screen = await context.OpenAsync((Guid.NewGuid(), Guid.NewGuid()));

        Assert.Contains("ShowCalendar", context.Navigator.Destinations);
        Assert.Empty(screen.Description);
    }

    /// <summary>The list is where it gets ticked off, which is the one thing this screen cannot do.</summary>
    [Fact]
    public async Task It_leads_back_to_the_list_the_entry_is_on()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel");
        var screen = await context.OpenAsync(opened);

        screen.ShowTaskListCommand.Execute(null);

        Assert.Equal(opened.TaskListLocalId, context.Navigator.LastTaskListId);
    }

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-15T10:00:00Z"));
        private readonly LocalTaskListRepository _taskLists;
        private readonly LocalCalendarEventRepository _events;
        private StubHttpMessageHandler? _nominatim;

        public ScreenContext()
        {
            _taskLists = new LocalTaskListRepository(_localStore, _clock, FixedNetworkStatus.Online);
            _events = new LocalCalendarEventRepository(_localStore, _clock, FixedNetworkStatus.Online);
        }

        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>What the address lookup answers with, in Nominatim's own shape - see PlaceSearch.</summary>
        public object Places { get; set; } = Array.Empty<object>();

        /// <summary>How many times the address was looked up, so "it did not have to be" can be asserted.</summary>
        public int LookupCount => _nominatim?.ReceivedRequests.Count ?? 0;

        public async Task<(Guid TaskListLocalId, Guid ItemId)> AddEntryAsync(
            string description, DateTime? due = null, string at = "", Guid? tiedTo = null)
        {
            var itemId = Guid.NewGuid();
            var dueUtc = due is { } localDue
                ? new DateTimeOffset(localDue, TimeZoneInfo.Local.GetUtcOffset(localDue)).ToUniversalTime()
                : (DateTimeOffset?)null;

            var created = await _taskLists.CreateAsync("Errands",
            [
                new TaskItemDto(
                    itemId, description, dueUtc, false, null, "None", false, "None", new TimeOnly(9, 0),
                    "Checklist", at, tiedTo)
            ]);

            return (created.LocalId, itemId);
        }

        /// <summary>
        /// An event the server knows about, which is what an entry's tie points at - the tie is stored
        /// as the event's own id.
        /// </summary>
        public async Task<Guid> AddEventAsync(string title, string address, double latitude, double longitude)
        {
            var start = _clock.GetUtcNow();
            var created = await _events.CreateAsync(new CalendarEventDetailsDto(
                title, null, new EventLocationDto(address, latitude, longitude), null,
                start, start.AddHours(1), false, null, [], [], "None", "None"));

            await using var dbContext = _localStore.CreateDbContext();
            var stored = dbContext.CalendarEvents.Single(candidate => candidate.LocalId == created.LocalId);
            stored.ServerId = Guid.NewGuid();
            await dbContext.SaveChangesAsync();
            return stored.ServerId.Value;
        }

        public async Task<TaskItemSummaryViewModel> OpenAsync((Guid TaskListLocalId, Guid ItemId) opened)
        {
            _nominatim = StubHttpMessageHandler.RespondingWith(Places);
            var screen = new TaskItemSummaryViewModel(
                _taskLists, _events, new PlaceSearch(_nominatim.ToHttpClient()),
                new Translations(new InMemoryLanguageStore()), Navigator);

            screen.Open(opened.TaskListLocalId, opened.ItemId);
            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        public void Dispose()
        {
            _nominatim?.Dispose();
            _localStore.Dispose();
        }
    }
}
