using Orbit.Mobile.Chat;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// Two flushes of the outgoing queue at once. The queue is flushed from two places - when somebody
/// presses Send, and from the conversation screen's poll every few seconds - so the two overlap
/// whenever a message is sent on the same tick as a poll.
///
/// This is the shape SyncGate was written for and the only entity type that did not go through it. Seen
/// on a device before it did: one tap, two copies of the message on the server, 88 milliseconds apart.
/// </summary>
public sealed class OverlappingSendTests
{
    [Fact]
    public async Task Two_flushes_at_once_do_not_send_the_same_message_twice()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();

        var held = new TaskCompletionSource();
        context.Server.HoldRequestsUntil = held;

        // Queued once, flushed twice - the send's own flush and a poll's, arriving together.
        var sending = context.Sender.SendAsync(context.OtherUserId, "on my way");
        var polling = context.Sender.FlushAsync();
        held.SetResult();
        await Task.WhenAll(sending, polling);

        Assert.Single(context.Server.Messages);
    }

    /// <summary>
    /// And the second flush is not simply dropped: a message queued while one is running still goes,
    /// because the run it waits for began before that message existed. SyncGate's own comment records
    /// what dropping cost when it was tried for the other entity types.
    /// </summary>
    [Fact]
    public async Task A_message_typed_while_a_flush_is_running_still_goes()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();

        var held = new TaskCompletionSource();
        context.Server.HoldRequestsUntil = held;

        var inFlight = context.Sender.FlushAsync();
        var sending = context.Sender.SendAsync(context.OtherUserId, "typed mid-flush");
        held.SetResult();
        await Task.WhenAll(inFlight, sending);

        Assert.Single(context.Server.Messages);
        Assert.Empty(await context.ReadQueuedAsync());
    }
}
