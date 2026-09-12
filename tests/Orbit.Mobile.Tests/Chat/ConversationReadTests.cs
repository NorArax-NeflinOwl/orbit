using Orbit.Contracts.Chat;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Chat;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// When the phone tells the server a conversation has been read. It used to be whenever the screen
/// synced - on opening, on every poll, and on polls that ran with the app in the background - so a
/// conversation left open in a pocket reported everything as read. Now it is what has been on screen,
/// with the screen showing and the app in front, and nothing past the last line shown. The page feeds
/// the last visible line in from CollectionView.Scrolled; everything after that is decided here, without
/// MAUI.
/// </summary>
public sealed class ConversationReadTests
{
    [Fact]
    public async Task Nothing_is_marked_before_anything_has_been_on_screen()
    {
        using var context = new ChatContext();
        Receive(context, "first");

        await OpenAsync(context);

        Assert.Null(context.Server.ReadUpToUtcForTheOtherParty(context.OwnUserId));
        Assert.Empty(context.Server.ConversationReadsUpTo);
    }

    [Fact]
    public async Task It_marks_up_to_the_last_line_on_screen_and_no_further()
    {
        using var context = new ChatContext();
        var first = Receive(context, "first");
        context.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = Receive(context, "second");
        var screen = await OpenAsync(context);

        await screen.ShowedUpToAsync(IndexOf(screen, first));
        Assert.Equal(first.SentAtUtc, context.Server.ReadUpToUtcForTheOtherParty(context.OwnUserId));

        await screen.ShowedUpToAsync(IndexOf(screen, second));
        Assert.Equal(second.SentAtUtc, context.Server.ReadUpToUtcForTheOtherParty(context.OwnUserId));
    }

    /// <summary>
    /// The page does not disappear when the app goes to the background, and its poll keeps running
    /// there - so this is what stops a phone in a pocket from reading. Coming back is what marks it.
    /// </summary>
    [Fact]
    public async Task An_app_in_the_background_marks_nothing_until_it_comes_back()
    {
        using var context = new ChatContext();
        var message = Receive(context, "are you there");
        var screen = await OpenAsync(context);

        screen.AppWentToBackground();
        await screen.ShowedUpToAsync(IndexOf(screen, message));
        await screen.LoadCommand.ExecuteAsync(null);
        Assert.Null(context.Server.ReadUpToUtcForTheOtherParty(context.OwnUserId));

        await screen.AppCameToForegroundAsync();
        Assert.Equal(message.SentAtUtc, context.Server.ReadUpToUtcForTheOtherParty(context.OwnUserId));
    }

    [Fact]
    public async Task A_screen_that_is_no_longer_showing_marks_nothing()
    {
        using var context = new ChatContext();
        var message = Receive(context, "are you there");
        var screen = await OpenAsync(context);

        screen.ScreenHidden();
        await screen.ShowedUpToAsync(IndexOf(screen, message));

        Assert.Null(context.Server.ReadUpToUtcForTheOtherParty(context.OwnUserId));
    }

    /// <summary>
    /// Offline is ordinary on a phone. A mark that could not go out is not remembered as told, so it goes
    /// at the next chance - here the next report of the same line - rather than being lost.
    /// </summary>
    [Fact]
    public async Task A_mark_made_offline_goes_out_once_the_server_can_be_reached()
    {
        using var context = new ChatContext();
        var message = Receive(context, "are you there");
        var screen = await OpenAsync(context);

        context.Server.IsUnreachable = true;
        await screen.ShowedUpToAsync(IndexOf(screen, message));
        context.Server.IsUnreachable = false;
        await screen.ShowedUpToAsync(IndexOf(screen, message));

        Assert.Equal(message.SentAtUtc, context.Server.ReadUpToUtcForTheOtherParty(context.OwnUserId));
    }

    /// <summary>The count on their row goes down to what has not been on screen yet, not to nought.</summary>
    [Fact]
    public async Task The_count_on_their_row_keeps_what_has_not_been_seen()
    {
        using var context = new ChatContext();
        await context.Repository.StoreContactsAsync([new ContactDto(
            context.OtherUserId, "bob", "Bob", "bob@example.com", context.OtherPublicKeyBase64,
            context.Clock.GetUtcNow(), RequiresApprovalFromCurrentUser: false,
            IsPendingApprovalFromOtherParty: false, UnreadCount: 2)]);
        var first = Receive(context, "first");
        context.Clock.Advance(TimeSpan.FromMinutes(1));
        Receive(context, "second");
        var screen = await OpenAsync(context);

        await screen.ShowedUpToAsync(IndexOf(screen, first));

        Assert.Equal(1, Assert.Single(await context.Repository.GetContactsAsync()).UnreadCount);
    }

    [Fact]
    public async Task A_group_is_marked_up_to_the_last_line_on_screen()
    {
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId, context.ThirdUserId);
        var first = ReceiveInGroup(context, group.Id, "first");
        context.Clock.Advance(TimeSpan.FromMinutes(1));
        ReceiveInGroup(context, group.Id, "second");
        var screen = await OpenGroupAsync(context, group.Id);

        await screen.ShowedUpToAsync(IndexOf(screen.Messages, first));

        Assert.Equal((group.Id, (DateTimeOffset?)first.SentAtUtc), Assert.Single(context.Server.GroupReadsUpTo));
    }

    [Fact]
    public async Task A_group_in_the_background_marks_nothing()
    {
        using var context = new ChatContext();
        context.PublishGroupMemberKeys();
        var group = context.Server.AddGroup("Trip", context.OtherUserId, context.ThirdUserId);
        var message = ReceiveInGroup(context, group.Id, "on my way");
        var screen = await OpenGroupAsync(context, group.Id);

        screen.AppWentToBackground();
        await screen.ShowedUpToAsync(IndexOf(screen.Messages, message));

        Assert.Empty(context.Server.GroupReadsUpTo);
    }

    /// <summary>
    /// The reader's own message at the bottom, an announcement, a line still waiting to send - none of
    /// them is what the server marks, so the answer is the other party's newest line above them.
    /// </summary>
    [Fact]
    public void Only_somebody_else_s_messages_count_towards_what_is_read()
    {
        var noon = DateTimeOffset.Parse("2026-09-11T12:00:00Z");
        var theirs = new ReadableChatMessage(false, "hi", noon, false, false, MessageId: Guid.NewGuid());
        IReadOnlyList<ReadableChatMessage> lines =
        [
            theirs,
            new ReadableChatMessage(false, null, noon.AddMinutes(1), false, false) { Announcement = "Carol joined" },
            new ReadableChatMessage(true, "hello", noon.AddMinutes(2), false, false, MessageId: Guid.NewGuid()),
            new ReadableChatMessage(true, "queued", noon.AddMinutes(3), false, IsWaitingToSend: true)
        ];
        var state = new ConversationReadState { IsShowing = true };

        state.ShowedUpTo(lines.Count - 1);

        Assert.Equal(theirs.SentAtUtc, state.ReadUpToToTell(lines));
    }

    /// <summary>A report from before the thread was redrawn shorter is nothing seen, not everything.</summary>
    [Fact]
    public void A_line_past_the_end_of_the_thread_counts_as_nothing_seen()
    {
        var line = new ReadableChatMessage(false, "hi", DateTimeOffset.Parse("2026-09-11T12:00:00Z"), false, false,
            MessageId: Guid.NewGuid());
        var state = new ConversationReadState { IsShowing = true };

        state.ShowedUpTo(3);

        Assert.Null(state.ReadUpToToTell([line]));
    }

    private static ChatMessageDto Receive(ChatContext context, string text)
    {
        var encrypted = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, text);
        return context.Server.AddIncoming(
            context.OtherUserId, context.OwnUserId, encrypted.CiphertextBase64, encrypted.NonceBase64);
    }

    private static ChatMessageDto ReceiveInGroup(ChatContext context, Guid groupId, string text)
    {
        var encrypted = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, text);
        return context.Server.AddIncomingGroupCopy(
            groupId, Guid.NewGuid(), context.OtherUserId, context.OwnUserId,
            encrypted.CiphertextBase64, encrypted.NonceBase64);
    }

    /// <summary>The screen as the page leaves it once it has appeared: loaded, and showing.</summary>
    private static async Task<ConversationViewModel> OpenAsync(ChatContext context)
    {
        var screen = context.Conversation();
        await screen.LoadCommand.ExecuteAsync(null);
        await screen.ScreenShownAsync();
        return screen;
    }

    private static async Task<GroupConversationViewModel> OpenGroupAsync(ChatContext context, Guid groupId)
    {
        var screen = new GroupConversationViewModel(
            context.Reader, context.Sender, context.Editor, context.Repository, context.Synchronizer,
            context.ChatClient, new Translations(new InMemoryLanguageStore()), new RecordingScreenNavigator(),
            context.LiveUpdates, context.Clock);

        await context.Synchronizer.SynchroniseGroupsAsync();
        screen.Open((await context.Repository.GetGroupsAsync()).Single(candidate => candidate.Id == groupId));
        await screen.LoadCommand.ExecuteAsync(null);
        await screen.ScreenShownAsync();
        return screen;
    }

    private static int IndexOf(ConversationViewModel screen, ChatMessageDto message)
        => IndexOf(screen.Messages, message);

    private static int IndexOf(IEnumerable<ReadableChatMessage> lines, ChatMessageDto message)
    {
        var index = lines.ToList().FindIndex(line => line.MessageId == message.Id);
        Assert.True(index >= 0, "The screen does not hold that message.");
        return index;
    }
}
