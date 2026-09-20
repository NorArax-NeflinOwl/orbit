using System.Globalization;
using Orbit.Mobile.Chat;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// What one message says about itself, which is what its menu's Info opens.
///
/// The thread stopped writing a time under every bubble on 2026-09-20 - a column of clock times down
/// the side of a conversation is noise, and the only question a thread at rest has to answer is how long
/// ago the last thing was said, so the newest message alone carries it (see ChatDayDividerTests). This
/// is where every other message still answers "when was that", and the only place a one-to-one
/// conversation says who has read it and when.
/// </summary>
public sealed class MessageInfoTests
{
    [Fact]
    public void It_says_when_the_message_was_sent()
    {
        using var context = new ChatContext();
        var screen = context.Conversation();
        var sentAtUtc = context.Clock.GetUtcNow();

        var said = screen.DescribeMessage(Message(sentAtUtc));

        // The reader's own clock and their own language, as the bubble's own stamp is - see
        // MessageTimestampTests. Spelled out rather than shortened: this is the screen somebody opens
        // *because* the short form was not enough.
        Assert.Contains(
            sentAtUtc.ToLocalTime().ToString("f", CultureInfo.GetCultureInfo("en-US")), said);
    }

    /// <summary>
    /// A message rewritten after it went out says so. The other side is reading something different
    /// from what they were sent, and until this nothing anywhere said so (reported 2026-09-20) - the
    /// bubble now carries a quiet "edited" and this says it in full.
    /// </summary>
    [Fact]
    public void It_says_that_a_message_was_rewritten_after_it_was_sent()
    {
        using var context = new ChatContext();
        var screen = context.Conversation();

        var said = screen.DescribeMessage(Message(context.Clock.GetUtcNow()) with { IsEdited = true });

        Assert.Contains("Rewritten after it was sent.", said);
    }

    [Fact]
    public void It_says_that_they_have_not_read_it_yet()
    {
        using var context = new ChatContext();
        var screen = context.Conversation();

        var said = screen.DescribeMessage(Message(context.Clock.GetUtcNow()));

        Assert.Contains("Bob hasn't read it yet.", said);
    }

    /// <summary>
    /// And once they have, by when. "Had read it by" rather than "read it at": the server keeps one
    /// mark per conversation rather than one per message, so what is known is the moment they were last
    /// reading - see ConversationViewModel.DescribeMessage.
    /// </summary>
    [Fact]
    public async Task It_says_by_when_they_had_read_it()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        await context.Sender.SendAsync(context.OtherUserId, "see you at six");
        context.Server.TheOtherPartyReadEverything(context.OtherUserId);

        var screen = context.Conversation();
        await screen.LoadCommand.ExecuteAsync(null);

        var mine = screen.Messages.Single(message => message.IsMine);
        Assert.True(mine.IsReadByThem);
        Assert.Contains("Bob had read it by", screen.DescribeMessage(mine));
    }

    /// <summary>Somebody else's message is not asked about being read - it is asked about who wrote it.</summary>
    [Fact]
    public void Somebody_elses_message_says_who_wrote_it()
    {
        using var context = new ChatContext();
        var screen = context.Conversation();

        var said = screen.DescribeMessage(Message(context.Clock.GetUtcNow()) with { IsMine = false });

        Assert.Contains("Written by Bob.", said);
        Assert.DoesNotContain("read it", said);
    }

    /// <summary>
    /// Which side of the bubble the "⋯" sits on. Beside the bubble rather than under it since
    /// 2026-09-20, and always on the inside of the thread: the reader's own messages hang off the right,
    /// so their trigger goes left of the bubble, and everybody else's the other way about.
    /// </summary>
    [Fact]
    public void The_menu_sits_on_the_inside_of_the_thread()
    {
        var mine = Message(DateTimeOffset.UtcNow) with { MessageId = Guid.NewGuid() };
        var theirs = mine with { IsMine = false };

        Assert.True(mine.HasActions);
        Assert.True(mine.MenuSitsOnTheLeft);
        Assert.False(mine.MenuSitsOnTheRight);

        Assert.True(theirs.HasActions);
        Assert.True(theirs.MenuSitsOnTheRight);
        Assert.False(theirs.MenuSitsOnTheLeft);
    }

    private static ReadableChatMessage Message(DateTimeOffset sentAtUtc)
        => new(IsMine: true, "see you at six", sentAtUtc, IsEdited: false, IsWaitingToSend: false);
}
