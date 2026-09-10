using Orbit.Contracts.Sharing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Screens.Sharing;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Something offered to this reader, on a screen of its own.
///
/// The notification for an offer took the last segment of its path - who made it - and opened the
/// conversation, where an offer arrives as a chat message with its own Accept. That works only as long
/// as the message can be read, and the case it cannot is the one this covers: the share row is on the
/// server whatever became of the message, so the offer can be read and taken up without it.
/// </summary>
public sealed class InvitationScreenTests
{
    private static readonly Guid ShareId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SharerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ItemId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void An_invitation_is_read_as_a_destination_of_its_own()
    {
        var destination = NotificationDestination.Parse($"/invitation/note/{ShareId}/{SharerUserId}");

        Assert.Equal(NotificationTarget.Invitation, destination!.Target);
        Assert.Equal(
            new InvitationOffer(SharedItemKind.Note, ShareId, SharerUserId), destination.Offer);
    }

    /// <summary>
    /// The segment names of the four kinds are the server's, and stable - see SharedItemPath. Reading
    /// one of them as another would ask the wrong endpoint for the offer and find nothing.
    /// </summary>
    [Theory]
    [InlineData("note", SharedItemKind.Note)]
    [InlineData("tasklist", SharedItemKind.TaskList)]
    [InlineData("event", SharedItemKind.CalendarEvent)]
    [InlineData("inventory", SharedItemKind.Inventory)]
    [InlineData("place", SharedItemKind.Place)]
    public void Each_kind_is_read_by_the_name_the_server_writes_into_the_path(
        string segment, SharedItemKind expected)
        => Assert.Equal(
            expected,
            NotificationDestination.Parse($"/invitation/{segment}/{ShareId}/{SharerUserId}")!.Offer!.Kind);

    /// <summary>
    /// A shared position is somebody's whereabouts rather than a thing an account can be given, and a
    /// kind added after this build is one this screen could say nothing about. Both fall back to what
    /// this path did before the screen existed - the conversation with whoever sent it, which is where
    /// the offer's own message is.
    /// </summary>
    [Theory]
    [InlineData("location")]
    [InlineData("something-newer")]
    public void A_kind_with_no_screen_still_opens_the_conversation(string segment)
    {
        var destination = NotificationDestination.Parse($"/invitation/{segment}/{ShareId}/{SharerUserId}");

        Assert.Equal(NotificationTarget.Conversation, destination!.Target);
        Assert.Equal(SharerUserId, destination.Id);
    }

    [Fact]
    public async Task Following_an_invitation_opens_the_screen_for_it_without_looking_anything_up()
    {
        using var context = new InvitationContext();
        var opener = Openers.AgainstNobody(context.LocalStore, context.Navigator);

        var outcome = await opener.OpenAsync($"/invitation/tasklist/{ShareId}/{SharerUserId}");

        Assert.Equal(NotificationOpenOutcome.Opened, outcome);
        Assert.Equal("ShowInvitation", context.Navigator.LastDestination);
        Assert.Equal(SharedItemKind.TaskList, context.Navigator.LastInvitation!.Kind);
        Assert.Equal(ShareId, context.Navigator.LastInvitation.ShareId);
    }

    [Fact]
    public async Task The_offer_is_read_under_the_kind_its_path_named()
    {
        using var context = new InvitationContext();
        context.Offer("Weekend jobs");
        var screen = context.Open(SharedItemKind.TaskList);

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.WasFound);
        Assert.Equal("Weekend jobs", screen.Title);
        Assert.Equal("Task list", screen.Kind);
        Assert.Contains("Ala", screen.SharedBy);
        Assert.True(screen.CanBeAccepted);
        Assert.Equal(["tasklist"], context.Shares.OffersRead);
    }

    /// <summary>
    /// Withdrawn, never made, or made to somebody else - the server answers all three the same way on
    /// purpose, and so does the screen.
    /// </summary>
    [Fact]
    public async Task An_offer_that_is_no_longer_there_says_so_and_offers_nothing()
    {
        using var context = new InvitationContext();
        var screen = context.Open(SharedItemKind.Note);

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(screen.WasFound);
        Assert.False(screen.CanBeAccepted);
        Assert.Contains("no longer there", screen.Message);
    }

    [Fact]
    public async Task Accepting_takes_it_up_at_the_endpoint_its_kind_belongs_to()
    {
        using var context = new InvitationContext();
        context.Offer("Shopping");
        var screen = context.Open(SharedItemKind.Note);
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.AcceptCommand.ExecuteAsync(null);

        Assert.Contains($"api/notes/shares/{ShareId}/accept", context.Shares.Accepted);
        // Not offered twice, and the screen says where it went rather than nothing at all.
        Assert.False(screen.CanBeAccepted);
        Assert.True(screen.IsHeld);
        Assert.Contains("Saved", screen.Message);
    }

    /// <summary>
    /// Taken up already - in the browser, or on this phone before the screen was reopened. There is
    /// nothing left to press, and saying so beats an Accept that answers with a refusal.
    /// </summary>
    [Fact]
    public async Task An_offer_already_taken_up_is_not_offered_again()
    {
        using var context = new InvitationContext();
        context.Offer("Shopping", isAccepted: true);
        var screen = context.Open(SharedItemKind.Note);

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(screen.CanBeAccepted);
        Assert.True(screen.IsHeld);
        Assert.Contains("already have", screen.Message);
    }

    /// <summary>
    /// The offer names the thing by the server's id and every screen here opens by the phone's own, so
    /// what is opened is the section - which pulls the new copy down as it loads.
    /// </summary>
    [Fact]
    public async Task Opening_what_was_accepted_goes_to_the_section_it_lands_in()
    {
        using var context = new InvitationContext();
        context.Offer("Kitchen", isAccepted: true);
        var screen = context.Open(SharedItemKind.Inventory);
        await screen.LoadCommand.ExecuteAsync(null);

        screen.OpenWhereItLandedCommand.Execute(null);

        Assert.Equal("ShowInventory", context.Navigator.LastDestination);
    }

    /// <summary>Where this notification led before the screen existed, kept as a way out of it.</summary>
    [Fact]
    public void Who_offered_it_is_still_a_press_away()
    {
        using var context = new InvitationContext();
        var screen = context.Open(SharedItemKind.Note);

        screen.MessageThemCommand.Execute(null);

        Assert.Equal("ShowContactInfo", context.Navigator.LastDestination);
        Assert.Equal(SharerUserId, context.Navigator.LastContactInfoUserId);
    }

    [Fact]
    public async Task An_offer_that_could_not_be_reached_says_so_rather_than_calling_it_withdrawn()
    {
        using var context = new InvitationContext();
        context.Shares.IsUnreachable = true;
        var screen = context.Open(SharedItemKind.Note);

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(screen.WasFound);
        Assert.Contains("Try again", screen.Message);
        Assert.DoesNotContain("no longer there", screen.Message);
    }

    private sealed class InvitationContext : IDisposable
    {
        public InvitationContext() => Users.Add(SharerUserId, "Ala", publicKeyBase64: null);

        public LocalStore LocalStore { get; } = new();

        public FakeShareServer Shares { get; } = new();

        public FakeUsersServer Users { get; } = new();

        public RecordingScreenNavigator Navigator { get; } = new();

        public void Offer(string title, bool isAccepted = false)
            => Shares.Offers[ShareId] = new ShareOfferDto(ItemId, title, isAccepted);

        public InvitationViewModel Open(SharedItemKind kind)
        {
            var shares = Shares.ToHttpClient();
            var screen = new InvitationViewModel(
                new ShareOfferClient(shares),
                new SharedItemAcceptance(
                    new NotesClient(shares), new TasksClient(shares), new CalendarClient(shares),
                    new InventoryClient(shares), new PlacesClient(shares)),
                new UsersClient(Users.ToHttpClient()),
                FixedNetworkStatus.Online,
                new Translations(new InMemoryLanguageStore()),
                Navigator);

            screen.Open(new InvitationOffer(kind, ShareId, SharerUserId));
            return screen;
        }

        public void Dispose() => LocalStore.Dispose();
    }
}
