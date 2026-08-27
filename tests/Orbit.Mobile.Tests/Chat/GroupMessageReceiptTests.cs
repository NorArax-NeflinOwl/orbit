using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Chat;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// What a group message's own info view says: who it reached, and who has opened it. The interesting
/// part is the difference between the two, because a receipt exists for every member the server holds a
/// copy for - so "delivered" and "not read" are the same row, and neither is a failure.
/// </summary>
public sealed class GroupMessageReceiptTests
{
    [Fact]
    public async Task Every_member_the_message_reached_is_listed()
    {
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId, context.ThirdUserId);
        await context.Sender.SendToGroupAsync(group.Id, "we leave at six");

        var described = await Describe(context, group.Id);

        Assert.Contains("Bob", described);
        Assert.Contains("Carol", described);
        // Not the sender: a receipt is a copy addressed to somebody else.
        Assert.DoesNotContain("You", described);
    }

    [Fact]
    public async Task A_member_who_has_not_opened_it_is_shown_as_delivered()
    {
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId, context.ThirdUserId);
        await context.Sender.SendToGroupAsync(group.Id, "we leave at six");

        var described = await Describe(context, group.Id);

        Assert.Contains("delivered", described);
        Assert.DoesNotContain("read", described);
    }

    [Fact]
    public async Task A_member_who_has_opened_it_is_shown_with_when()
    {
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId, context.ThirdUserId);
        await context.Sender.SendToGroupAsync(group.Id, "we leave at six");

        var groupMessageId = context.Server.GroupMessageCopies[0].GroupMessageId!.Value;
        context.Server.MarkGroupMessageRead(groupMessageId, context.OtherUserId, context.Clock.GetUtcNow());

        var described = await Describe(context, group.Id);

        Assert.Contains("read", described);
        // The one who has not opened it is still there, still only delivered.
        Assert.Contains("delivered", described);
    }

    /// <summary>
    /// Being out of reach is not "nobody has read it" - the honest answer is that it could not be
    /// asked, which is what this says instead of an empty list.
    /// </summary>
    [Fact]
    public async Task Being_unable_to_ask_says_so_rather_than_showing_nothing()
    {
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId, context.ThirdUserId);
        await context.Sender.SendToGroupAsync(group.Id, "we leave at six");

        // Opened while the server was still there, so there is a message to ask about - the asking
        // is what fails.
        var screen = await Open(context, group.Id);
        context.Server.IsUnreachable = true;

        var described = await screen.DescribeReceiptsAsync(screen.Messages.Single(message => message.IsInAGroup));

        Assert.Equal("Couldn't read who has seen this.", described);
    }

    private static async Task<string> Describe(ChatContext context, Guid groupId)
    {
        var screen = await Open(context, groupId);
        return await screen.DescribeReceiptsAsync(screen.Messages.Single(message => message.IsInAGroup));
    }

    private static async Task<GroupConversationViewModel> Open(ChatContext context, Guid groupId)
    {
        var screen = new GroupConversationViewModel(
            context.Reader, context.Sender, context.Editor, context.Repository, context.Synchronizer,
            context.ChatClient, new Translations(new InMemoryLanguageStore()), new RecordingScreenNavigator());

        await context.Synchronizer.SynchroniseGroupsAsync();
        var stored = (await context.Repository.GetGroupsAsync()).Single(candidate => candidate.Id == groupId);
        screen.Open(stored);
        await screen.LoadCommand.ExecuteAsync(null);

        return screen;
    }
}
