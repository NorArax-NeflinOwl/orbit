using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Tags;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Tags;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Tags;

/// <summary>
/// The account's tag colours on the phone - set here first and sent from here, like every other write, and
/// brought back from the server over everything no longer waiting. See LocalTagColourRepository and
/// TagColourSynchronizer; the fake refuses what the server refuses.
/// </summary>
public sealed class TagColourSyncTests : IDisposable
{
    private readonly LocalStore _store = new();
    private readonly FakeTagsServer _server = new();
    private readonly LocalTagColourRepository _colours;
    private readonly TagColourSynchronizer _synchronizer;

    public TagColourSyncTests()
    {
        _colours = new LocalTagColourRepository(_store);
        _synchronizer = new TagColourSynchronizer(
            _colours, new TagsClient(_server.ToHttpClient()), NullLogger<TagColourSynchronizer>.Instance);
    }

    public void Dispose()
    {
        _store.Dispose();
        _server.Dispose();
    }

    /// <summary>
    /// On a train: the colour is kept and drawn at once, and the sync that cannot reach the server loses
    /// nothing - the next one sends it.
    /// </summary>
    [Fact]
    public async Task A_colour_picked_offline_is_kept_and_sent_at_the_next_sync()
    {
        _server.IsUnreachable = true;
        await _colours.SetAsync("Work", "#AA3355");

        await Assert.ThrowsAsync<HttpRequestException>(() => _synchronizer.SynchroniseAsync());
        Assert.Equal("#aa3355", (await _colours.ColoursAsync())["work"]);

        _server.IsUnreachable = false;
        await _synchronizer.SynchroniseAsync();

        Assert.Equal(new TagColourDto("Work", "#aa3355"), Assert.Single(_server.Colours));
        Assert.Empty(await _colours.PendingAsync());
    }

    [Fact]
    public async Task A_colour_set_on_another_device_arrives_and_one_taken_away_there_goes()
    {
        _server.ColourElsewhere("home", "#113355");
        await _synchronizer.SynchroniseAsync();
        Assert.Equal("#113355", (await _colours.ColoursAsync())["home"]);

        _server.TakeAwayElsewhere("home");
        await _synchronizer.SynchroniseAsync();

        Assert.Empty(await _colours.ColoursAsync());
    }

    /// <summary>A colour picked here and not sent yet is newer than the server's, so a pull leaves it alone.</summary>
    [Fact]
    public async Task A_colour_waiting_to_be_sent_is_not_overwritten_by_the_servers_older_one()
    {
        await _colours.SetAsync("Work", "#aa3355");

        await _colours.ReplaceFromServerAsync([new TagColourDto("work", "#113355")]);

        Assert.Equal("#aa3355", (await _colours.ColoursAsync())["work"]);
        Assert.Single(await _colours.PendingAsync());
    }

    /// <summary>
    /// One the server will never take is dropped - not sent and refused on every sync for ever - and the
    /// server's own answer takes its place.
    /// </summary>
    [Fact]
    public async Task A_colour_the_server_refuses_is_dropped_rather_than_sent_for_ever()
    {
        await _colours.SetAsync("work", "red");

        var first = await _synchronizer.SynchroniseAsync();
        await _synchronizer.SynchroniseAsync();

        Assert.Equal(1, first.GivenUp);
        Assert.Single(_server.Sets);
        Assert.Empty(await _colours.PendingAsync());
        Assert.Empty(_server.Colours);
    }

    [Fact]
    public void A_list_row_draws_its_tags_in_the_accounts_colours_whatever_their_case()
    {
        var row = Row(new LocalTaskList { Title = "Errands", Tags = ["Work", "home"] }, privateItemsAreUnlocked: true);

        Assert.Equal(
            [new TagChip("Work", "#aa3355"), new TagChip("home", string.Empty)],
            row.Tags.Chips);
    }

    /// <summary>A private list's tags are sealed with it, so a locked row says nothing about them either.</summary>
    [Fact]
    public void A_private_list_hidden_while_locked_shows_no_tags()
    {
        var row = Row(
            new LocalTaskList { Title = "Bank", IsPrivate = true, Tags = ["money"] }, privateItemsAreUnlocked: false);

        Assert.False(row.Tags.HasAny);
    }

    /// <summary>
    /// A note or a list this phone held before tags existed has tags nobody here knows. Its form says
    /// nothing about them on a save - null, "not provided" - until somebody touches the box, so saving it
    /// cannot empty tags written in a browser.
    /// </summary>
    [Fact]
    public async Task The_tags_form_says_nothing_about_tags_it_never_knew_until_they_are_touched()
    {
        var form = new TagsForm(new Translations(new InMemoryLanguageStore()));

        await form.ShowAsync(null, []);
        Assert.Null(form.ToSave);

        form.Text = "work, Work, home";
        Assert.Equal(["work", "home"], form.ToSave);

        await form.ShowAsync([], []);
        Assert.Equal([], form.ToSave);
    }

    private static TaskListRow Row(LocalTaskList taskList, bool privateItemsAreUnlocked)
        => TaskListRow.From(
            taskList, [taskList], hasUnsentChanges: false, FixedNetworkStatus.Online,
            new Translations(new InMemoryLanguageStore()), privateItemsAreUnlocked,
            tagColours: new Dictionary<string, string> { ["work"] = "#aa3355" });
}
