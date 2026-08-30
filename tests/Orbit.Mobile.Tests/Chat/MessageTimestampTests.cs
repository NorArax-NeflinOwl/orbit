using System.Globalization;
using Orbit.Localization;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// When a message says it was sent.
///
/// The two conversation pages were the only screens handing a raw UTC value to XAML to format
/// (<c>StringFormat='{0:g}'</c>), which got both halves wrong at once: a DateTimeOffset writes itself in
/// its own offset, and XAML formats against the phone's culture rather than the reader's language. A
/// message sent at 14:41 in Warsaw read "12:41 PM" on a Polish screen. Found on a device.
/// </summary>
public sealed class MessageTimestampTests
{
    [Fact]
    public async Task A_message_says_when_it_was_sent_on_the_readers_own_clock()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        await context.Sender.SendAsync(context.OtherUserId, "Hello");

        var message = Assert.Single(await context.ReadConversationAsync());

        // The clock is fixed at 10:00 UTC, so what this should say depends on where the phone is - which
        // is the point: it must be that, and not the UTC the message is stored in.
        var sentAtUtc = context.Clock.GetUtcNow();
        Assert.Equal(sentAtUtc.ToLocalTime().ToString("g", CultureInfo.GetCultureInfo("en-US")), message.SentAt);
    }

    /// <summary>
    /// And in the reader's language. Asserted by shape rather than against a fixed string, so the test
    /// says nothing about which timezone it runs in: Polish writes the hour on a 24-hour clock, so an
    /// AM or a PM in there means the phone's culture won again.
    /// </summary>
    [Fact]
    public async Task A_polish_reader_is_told_the_time_in_polish()
    {
        using var context = new ChatContext();
        context.Translations.SetLanguage(AppLanguage.Polish);
        context.GiveTheOtherPartyAPublishedKey();
        await context.Sender.SendAsync(context.OtherUserId, "Cześć");

        var message = Assert.Single(await context.ReadConversationAsync());

        Assert.DoesNotContain("AM", message.SentAt);
        Assert.DoesNotContain("PM", message.SentAt);
        Assert.Contains(".", message.SentAt);
    }

    /// <summary>
    /// A message still waiting to go out says when it was written, by the same clock and in the same
    /// language - it is on screen among the sent ones, and a timestamp that jumped when it went out
    /// would read as the message having been rewritten.
    /// </summary>
    [Fact]
    public async Task A_message_waiting_to_go_out_is_stamped_the_same_way()
    {
        using var context = new ChatContext();
        context.Server.IsUnreachable = true;

        await context.Sender.SendAsync(context.OtherUserId, "Written on a train");

        var message = Assert.Single(await context.ReadConversationAsync());
        Assert.True(message.IsWaitingToSend);
        Assert.Equal(
            context.Clock.GetUtcNow().ToLocalTime().ToString("g", CultureInfo.GetCultureInfo("en-US")),
            message.SentAt);
    }
}
