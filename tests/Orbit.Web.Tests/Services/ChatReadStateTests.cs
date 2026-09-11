using Orbit.Contracts.Chat;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// What an open thread tells the server its reader has seen. The thread-level half - that nothing is
/// asked for behind another tab or an unfocused window - is in ChatThreadTests; this is the arithmetic.
/// </summary>
public sealed class ChatReadStateTests
{
    private static readonly Guid OwnUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly DateTimeOffset Noon = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Nothing_seen_tells_nothing()
    {
        var messages = new[] { Theirs(minutesAgo: 1) };

        Assert.Null(new ChatReadState().ReadUpToToTell(null, messages, OwnUserId));
    }

    [Fact]
    public void A_message_the_thread_does_not_hold_tells_nothing()
    {
        var messages = new[] { Theirs(minutesAgo: 1) };

        Assert.Null(new ChatReadState().ReadUpToToTell(Guid.NewGuid(), messages, OwnUserId));
    }

    [Fact]
    public void It_is_up_to_the_message_in_view_and_not_past_it()
    {
        var inView = Theirs(minutesAgo: 2);
        var below = Theirs(minutesAgo: 1);

        Assert.Equal(
            inView.SentAtUtc, new ChatReadState().ReadUpToToTell(inView.Id, [inView, below], OwnUserId));
    }

    /// <summary>
    /// The reader's own message at the bottom says nothing the server needs - what it marks is the other
    /// party's, so the answer is the newest of theirs above it.
    /// </summary>
    [Fact]
    public void An_own_message_in_view_tells_up_to_the_other_partys_newest_before_it()
    {
        var theirs = Theirs(minutesAgo: 3);
        var own = Own(minutesAgo: 1);

        Assert.Equal(theirs.SentAtUtc, new ChatReadState().ReadUpToToTell(own.Id, [theirs, own], OwnUserId));
    }

    [Fact]
    public void A_thread_of_only_own_messages_tells_nothing()
    {
        var own = Own(minutesAgo: 1);

        Assert.Null(new ChatReadState().ReadUpToToTell(own.Id, [own], OwnUserId));
    }

    [Fact]
    public void What_the_server_accepted_is_not_told_again()
    {
        var message = Theirs(minutesAgo: 1);
        var state = new ChatReadState();
        state.Told(state.ReadUpToToTell(message.Id, [message], OwnUserId)!.Value);

        Assert.Null(state.ReadUpToToTell(message.Id, [message], OwnUserId));
    }

    [Fact]
    public void Scrolling_back_up_makes_nothing_unread_again()
    {
        var older = Theirs(minutesAgo: 2);
        var newer = Theirs(minutesAgo: 1);
        var state = new ChatReadState();
        state.Told(newer.SentAtUtc);

        Assert.Null(state.ReadUpToToTell(older.Id, [older, newer], OwnUserId));
    }

    [Fact]
    public void A_newer_message_seen_after_a_told_one_is_told()
    {
        var older = Theirs(minutesAgo: 2);
        var newer = Theirs(minutesAgo: 1);
        var state = new ChatReadState();
        state.Told(older.SentAtUtc);

        Assert.Equal(newer.SentAtUtc, state.ReadUpToToTell(newer.Id, [older, newer], OwnUserId));
    }

    /// <summary>A mark that never reached the server is offered again, rather than believed.</summary>
    [Fact]
    public void A_mark_that_was_never_accepted_is_offered_again()
    {
        var message = Theirs(minutesAgo: 1);
        var state = new ChatReadState();
        state.ReadUpToToTell(message.Id, [message], OwnUserId);

        Assert.Equal(message.SentAtUtc, state.ReadUpToToTell(message.Id, [message], OwnUserId));
    }

    /// <summary>What clears the bell's entries about a conversation: nothing of theirs left below what was seen.</summary>
    [Fact]
    public void Their_newest_in_view_is_their_newest_seen()
    {
        var older = Theirs(minutesAgo: 2);
        var newer = Theirs(minutesAgo: 1);

        Assert.True(ChatReadState.HasSeenTheirNewest(newer.Id, [older, newer], OwnUserId));
    }

    [Fact]
    public void A_message_of_theirs_below_the_one_in_view_is_not_seen()
    {
        var older = Theirs(minutesAgo: 2);
        var newer = Theirs(minutesAgo: 1);

        Assert.False(ChatReadState.HasSeenTheirNewest(older.Id, [older, newer], OwnUserId));
    }

    /// <summary>The reader's own reply under their last message is past it, so theirs has been seen.</summary>
    [Fact]
    public void An_own_message_in_view_below_theirs_means_theirs_was_seen()
    {
        var theirs = Theirs(minutesAgo: 3);
        var own = Own(minutesAgo: 1);

        Assert.True(ChatReadState.HasSeenTheirNewest(own.Id, [theirs, own], OwnUserId));
    }

    [Fact]
    public void Nothing_in_view_is_nothing_seen()
    {
        var messages = new[] { Theirs(minutesAgo: 1) };

        Assert.False(ChatReadState.HasSeenTheirNewest(null, messages, OwnUserId));
        Assert.False(ChatReadState.HasSeenTheirNewest(Guid.NewGuid(), messages, OwnUserId));
    }

    private static ChatMessageDto Theirs(int minutesAgo)
        => new(Guid.NewGuid(), OtherUserId, OwnUserId, "sealed", "nonce", Noon.AddMinutes(-minutesAgo), false, null);

    private static ChatMessageDto Own(int minutesAgo)
        => new(Guid.NewGuid(), OwnUserId, OtherUserId, "sealed", "nonce", Noon.AddMinutes(-minutesAgo), false, null);
}
