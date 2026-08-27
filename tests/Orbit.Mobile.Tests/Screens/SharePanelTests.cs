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

    private static async Task GiveAContactAsync(ChatContext context, string displayName)
    {
        context.GiveTheOtherPartyAPublishedKey();
        var contact = context.Server.Contacts.Single(candidate => candidate.UserId == context.OtherUserId);
        await context.Repository.StoreContactsAsync([contact with { DisplayName = displayName }]);
    }

    private static SharePanel Build(
        ChatContext context, FakeShareServer shares, Orbit.Mobile.Permissions.UserPermissions? permissions = null)
    {
        var http = shares.ToHttpClient();

        return new SharePanel(
            context.Repository,
            new SharedItemSharing(
                new NotesClient(http), new TasksClient(http), new CalendarClient(http), new InventoryClient(http),
                context.Sender),
            permissions ?? UnlockedPermissions.For(new LocalStore()),
            new Translations(new InMemoryLanguageStore()));
    }
}
