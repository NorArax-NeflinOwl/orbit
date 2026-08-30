using Orbit.Core.Abstractions;
using Orbit.Core.Permissions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Sharing;
using Orbit.Mobile.Tests.Chat;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Offering the thing on screen to somebody else. Two things have to happen and both matter: the server
/// records the offer, and a chat message carries its id to the recipient. The phone could do neither -
/// the whole Sharing permission existed with nothing behind it.
/// </summary>
public sealed class SharePanelTests
{
    [Fact]
    public async Task Opening_it_lists_the_people_there_are_to_share_with()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();
        await GiveAContactAsync(context, "Anna");
        var panel = Build(context, shares);

        await panel.OpenCommand.ExecuteAsync(null);

        Assert.True(panel.IsOpen);
        Assert.Equal(["Anna"], panel.Recipients.Select(contact => contact.DisplayName));
    }

    /// <summary>
    /// Sharing goes to somebody you have a conversation with, so with nobody to share with the panel
    /// says so rather than opening onto an empty list.
    /// </summary>
    [Fact]
    public async Task With_nobody_to_share_with_it_says_so_and_stays_shut()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();
        var panel = Build(context, shares);

        await panel.OpenCommand.ExecuteAsync(null);

        Assert.False(panel.IsOpen);
        Assert.True(panel.HasMessage);
    }

    [Fact]
    public async Task Sharing_records_the_offer_and_tells_them()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();
        await GiveAContactAsync(context, "Anna");
        var panel = Build(context, shares);
        panel.Describes(SharedItemKind.Note, Guid.NewGuid(), "Shopping");
        await panel.OpenCommand.ExecuteAsync(null);
        panel.Recipient = panel.Recipients.Single();

        await panel.SendCommand.ExecuteAsync(null);

        // The server's half: an offer recorded against the note.
        Assert.Contains(shares.Accepted, path => path.StartsWith("api/notes/") && path.EndsWith("/shares"));

        // The client's half - a message only a client can send, because it is end-to-end encrypted.
        var sent = Assert.Single(context.Server.Messages);
        var invitation = SharedItemInvitation.TryRead(context.OpenAsTheOtherParty(sent)!);
        Assert.Equal(SharedItemKind.Note, invitation!.Kind);
        Assert.Equal("Shopping", invitation.Name);
    }

    [Fact]
    public async Task Nothing_is_shared_until_somebody_is_chosen()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();
        await GiveAContactAsync(context, "Anna");
        var panel = Build(context, shares);

        await panel.OpenCommand.ExecuteAsync(null);

        Assert.False(panel.SendCommand.CanExecute(null));
    }

    /// <summary>Read-only unless somebody says otherwise, which is what the server defaults to as well.</summary>
    [Fact]
    public void It_offers_read_only_first()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();

        Assert.Equal(ShareAccessLevel.ReadOnly, Build(context, shares).AccessLevel!.Value);
    }

    /// <summary>
    /// An account that has not unlocked sharing is not offered it - the endpoints would refuse the
    /// attempt anyway, and a button that only ever fails is not a button.
    /// </summary>
    [Fact]
    public async Task An_account_without_the_permission_is_not_offered_sharing()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();
        using var localStore = new LocalStore();

        var panel = Build(context, shares, await UnlockedPermissions.LockedTo(localStore, ApplicationPermission.Chat));

        Assert.False(panel.CanShare);
    }

    /// <summary>
    /// Having the permission is not enough - there has to be something to offer. The panel binds its own
    /// visibility to this, which is why the question is asked here: an IsVisible set on the instance by
    /// an editor's markup is overridden by the panel's own binding and does nothing at all. Found on a
    /// device, with the share buttons still sitting under a note that had just been made private.
    /// </summary>
    [Fact]
    public void Nothing_is_offered_until_an_editor_says_what_it_has()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();

        var panel = Build(context, shares);

        Assert.False(panel.CanShare);
    }

    [Fact]
    public void Something_that_can_no_longer_be_offered_stops_being_offered()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();
        var panel = Build(context, shares);
        panel.Describes(SharedItemKind.Note, Guid.NewGuid(), "Shopping");

        panel.OffersNothing();

        Assert.False(panel.CanShare);
    }

    [Fact]
    public void An_editor_that_says_what_it_has_is_offered_sharing()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();
        var panel = Build(context, shares);

        panel.Describes(SharedItemKind.Note, Guid.NewGuid(), "Shopping");

        Assert.True(panel.CanShare);
    }

    [Fact]
    public async Task A_link_is_built_around_the_token_and_offered()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();
        using var links = new FakePublicShareServer { WebAddress = "https://orbit.example/" };
        var panel = Build(context, shares, links: links);
        panel.Describes(SharedItemKind.Note, Guid.NewGuid(), "Shopping");
        string? offered = null;
        panel.LinkReady += (_, address) => offered = address;

        await panel.CreateLinkCommand.ExecuteAsync(null);

        Assert.StartsWith("https://orbit.example/s/", panel.LinkAddress);
        Assert.Equal(panel.LinkAddress, offered);
    }

    /// <summary>
    /// A second link would leave the first working, so revoking would then stop only one of them. The
    /// panel asks whether there is one before making another.
    /// </summary>
    [Fact]
    public async Task Asking_twice_reuses_the_link_it_already_made()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();
        using var links = new FakePublicShareServer();
        var panel = Build(context, shares, links: links);
        panel.Describes(SharedItemKind.Note, Guid.NewGuid(), "Shopping");

        await panel.CreateLinkCommand.ExecuteAsync(null);
        var first = panel.LinkAddress;
        await panel.CreateLinkCommand.ExecuteAsync(null);

        Assert.Equal(first, panel.LinkAddress);
        Assert.Equal(1, links.LinksCreated);
    }

    [Fact]
    public async Task Stopping_the_link_forgets_it()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();
        using var links = new FakePublicShareServer();
        var panel = Build(context, shares, links: links);
        panel.Describes(SharedItemKind.Note, Guid.NewGuid(), "Shopping");
        await panel.CreateLinkCommand.ExecuteAsync(null);

        await panel.RevokeLinkCommand.ExecuteAsync(null);

        Assert.False(panel.HasLink);
    }

    /// <summary>
    /// A deployment that has not said where its browser client lives cannot have a link built for it.
    /// Saying so beats handing somebody a URL that goes nowhere.
    /// </summary>
    [Fact]
    public async Task With_no_web_address_it_says_so_rather_than_building_a_broken_link()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();
        using var links = new FakePublicShareServer { WebAddress = string.Empty };
        var panel = Build(context, shares, links: links);
        panel.Describes(SharedItemKind.Note, Guid.NewGuid(), "Shopping");

        await panel.CreateLinkCommand.ExecuteAsync(null);

        Assert.False(panel.HasLink);
        Assert.True(panel.HasMessage);
    }

    /// <summary>
    /// Only for something that arrived through somebody else's share and cannot be changed. Your own
    /// things need no permission, and one you can already edit needs no more.
    /// </summary>
    [Fact]
    public void There_is_nobody_to_ask_about_your_own_thing()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();
        var panel = Build(context, shares);

        panel.Describes(SharedItemKind.Note, Guid.NewGuid(), "Shopping");

        Assert.False(panel.CanAskToEdit);
    }

    [Fact]
    public async Task Asking_to_edit_sends_the_owner_a_message()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();
        await GiveAContactAsync(context, "Anna");
        var panel = Build(context, shares);
        var itemId = Guid.NewGuid();
        panel.Describes(SharedItemKind.Note, itemId, "Shopping", context.OtherUserId);

        Assert.True(panel.CanAskToEdit);
        await panel.AskToEditCommand.ExecuteAsync(null);

        var sent = Assert.Single(context.Server.Messages);
        var request = EditAccessRequest.TryRead(context.OpenAsTheOtherParty(sent)!);
        Assert.Equal(itemId, request!.ItemId);
        Assert.Equal("Shopping", request.Name);
    }

    /// <summary>Asking twice says the same thing twice, so the button goes once it has been used.</summary>
    [Fact]
    public async Task It_can_only_be_asked_once()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();
        await GiveAContactAsync(context, "Anna");
        var panel = Build(context, shares);
        panel.Describes(SharedItemKind.Note, Guid.NewGuid(), "Shopping", context.OtherUserId);

        await panel.AskToEditCommand.ExecuteAsync(null);

        Assert.False(panel.CanAskToEdit);
    }

    /// <summary>
    /// Found by doing it on a phone: message somebody for the first time, open a note, and sharing said
    /// there was nobody to share with - having just been told to start a conversation, which is what had
    /// been done. The cached list is only filled by the contacts screen, so anyone met since was invisible
    /// here until that screen happened to be visited.
    /// </summary>
    [Fact]
    public async Task Somebody_met_since_the_cache_was_last_filled_can_still_be_shared_with()
    {
        using var context = new ChatContext();
        using var shares = new FakeShareServer();
        // On the server only. Nothing is stored locally, which is the state after a first message.
        context.GiveTheOtherPartyAPublishedKey();
        var panel = Build(context, shares);

        await panel.OpenCommand.ExecuteAsync(null);

        Assert.True(panel.IsOpen);
        Assert.NotEmpty(panel.Recipients);
    }

    private static async Task GiveAContactAsync(ChatContext context, string displayName)
    {
        context.GiveTheOtherPartyAPublishedKey();

        // Renamed on the server as well as in the cache. Opening the panel refreshes the contact list
        // from the server now, and a name that existed only locally would be replaced on the way in -
        // which is the point of the refresh, the server's list being the complete answer.
        var index = context.Server.Contacts.FindIndex(candidate => candidate.UserId == context.OtherUserId);
        context.Server.Contacts[index] = context.Server.Contacts[index] with { DisplayName = displayName };
        await context.Repository.StoreContactsAsync([context.Server.Contacts[index]]);
    }

    private static SharePanel Build(
        ChatContext context, FakeShareServer shares, Orbit.Mobile.Permissions.UserPermissions? permissions = null,
        FakePublicShareServer? links = null)
    {
        var http = shares.ToHttpClient();

        return new SharePanel(
            context.Repository,
            context.Synchronizer,
            new SharedItemSharing(
                new NotesClient(http), new TasksClient(http), new CalendarClient(http), new InventoryClient(http),
                context.Sender),
            new PublicShareClient((links ?? new FakePublicShareServer()).ToHttpClient()),
            permissions ?? UnlockedPermissions.For(new LocalStore()),
            new Translations(new InMemoryLanguageStore()));
    }
}
