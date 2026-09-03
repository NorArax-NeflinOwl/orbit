using Orbit.Contracts.Sharing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Screens.Sharing;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Following a public link into the app instead of into a browser.
///
/// The screen behind it reads something that may belong to a stranger and is in no account on this
/// phone, which is what makes it unlike every other screen here: nothing is looked up locally, nothing
/// can be edited, and the one action needs an account because a copy has to belong to somebody.
/// </summary>
public sealed class SharedLinkScreenTests
{
    private const string Token = "a-link-token";

    [Fact]
    public void A_link_is_read_as_a_destination_of_its_own()
    {
        // The path Orbit.Web serves a link at, which is what Android hands the app - see MainActivity.
        var destination = NotificationDestination.Parse("/s/a-link-token");

        Assert.Equal(NotificationTarget.SharedLink, destination!.Target);
        Assert.Equal("a-link-token", destination.Token);
    }

    /// <summary>A token is not an id and not a Guid, so nothing tries to parse it as one.</summary>
    [Fact]
    public void A_token_that_looks_like_nothing_in_particular_still_opens()
    {
        Assert.Equal("Zx-9_aB", NotificationDestination.Parse("/s/Zx-9_aB")!.Token);
    }

    [Fact]
    public async Task Following_a_link_opens_the_screen_for_it_without_looking_anything_up()
    {
        var context = new SharedLinkContext();
        var opener = Openers.AgainstNobody(context.LocalStore, context.Navigator);

        var outcome = await opener.OpenAsync($"/s/{Token}");

        Assert.Equal(NotificationOpenOutcome.Opened, outcome);
        Assert.Equal("ShowSharedLink", context.Navigator.LastDestination);
        Assert.Equal(Token, context.Navigator.LastSharedLinkToken);
    }

    [Fact]
    public async Task What_is_behind_the_link_is_shown_as_it_was_shared()
    {
        var context = new SharedLinkContext();
        context.Publish(new PublicSharedItemDto(
            "TaskList", "Weekend jobs", "Four things", [
                new PublicSharedItemLineDto("Post the letter", IsChecklistItem: true, IsChecked: true, Detail: null),
                new PublicSharedItemLineDto("Buy milk", IsChecklistItem: true, IsChecked: false, Detail: "by 5pm")
            ], "Ala", DateTimeOffset.UtcNow));
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.WasFound);
        Assert.Equal("Weekend jobs", screen.Title);
        Assert.Equal("Four things", screen.Subtitle);
        Assert.Equal("Task list", screen.Kind);
        Assert.Contains("Ala", screen.SharedBy);
        Assert.Equal(["Post the letter", "Buy milk"], screen.Lines.Select(line => line.Text));
        Assert.True(screen.Lines[0].IsTicked);
        Assert.Equal("by 5pm", screen.Lines[1].Detail);
    }

    /// <summary>
    /// Withdrawn, never existed, or pointing at something now deleted - all one sentence. Telling them
    /// apart would say more about somebody else's account than a stranger holding a link should learn.
    /// </summary>
    [Fact]
    public async Task A_link_that_does_not_work_says_so_without_saying_why()
    {
        var context = new SharedLinkContext();
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(screen.WasFound);
        Assert.Contains("doesn't work", screen.Title);
        Assert.NotEmpty(screen.Message);
        Assert.False(screen.CanBeKept);
    }

    [Fact]
    public async Task Keeping_it_takes_a_copy_into_this_account()
    {
        var context = new SharedLinkContext();
        context.PublishANote();
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.CanBeKept);

        await screen.KeepCommand.ExecuteAsync(null);

        Assert.Equal(1, context.Server.TimesClaimed);
        // Not offered twice: there is nothing a second copy would add.
        Assert.False(screen.CanBeKept);
        Assert.Contains("Saved", screen.Message);
    }

    [Fact]
    public async Task Something_this_account_already_has_says_that_rather_than_saved()
    {
        var context = new SharedLinkContext();
        context.PublishANote();
        context.Server.ClaimFindsItAlreadyHeld = true;
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.KeepCommand.ExecuteAsync(null);

        Assert.Contains("already have", screen.Message);
    }

    /// <summary>
    /// Reading needs no account - that is what a public link is for - so the screen shows the thing and
    /// simply does not offer to keep it.
    /// </summary>
    [Fact]
    public async Task Somebody_not_signed_in_can_read_it_but_is_not_offered_a_copy()
    {
        var context = new SharedLinkContext(signedIn: false);
        context.PublishANote();
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.WasFound);
        Assert.False(screen.CanBeKept);
    }

    [Fact]
    public async Task A_link_that_could_not_be_reached_says_so_rather_than_calling_it_broken()
    {
        var context = new SharedLinkContext();
        context.Server.IsUnreachable = true;
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(screen.WasFound);
        Assert.Contains("Try again", screen.Message);
    }

    private sealed class SharedLinkContext : IDisposable
    {
        public SharedLinkContext(bool signedIn = true)
        {
            Storage = new InMemorySessionStorage(signedIn
                ? new UserSession("access", "refresh", Guid.NewGuid(), "me@orbit.example", "Me")
                : null);
        }

        public LocalStore LocalStore { get; } = new();

        public FakePublicShareServer Server { get; } = new();

        public RecordingScreenNavigator Navigator { get; } = new();

        private InMemorySessionStorage Storage { get; }

        public void Publish(PublicSharedItemDto item) => Server.Published[Token] = item;

        public void PublishANote()
            => Publish(new PublicSharedItemDto(
                "Note", "A shared note", null, [new PublicSharedItemLineDto("Some words", false, false, null)],
                "Ala", DateTimeOffset.UtcNow));

        public SharedLinkViewModel Open()
        {
            var screen = new SharedLinkViewModel(
                new PublicShareClient(Server.ToHttpClient()), new SessionStore(Storage),
                FixedNetworkStatus.Online, new Translations(new InMemoryLanguageStore()), Navigator);

            screen.Open(Token);
            return screen;
        }

        public void Dispose() => LocalStore.Dispose();
    }
}
