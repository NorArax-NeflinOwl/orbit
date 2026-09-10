using System.Globalization;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// Where one day's messages end and the next day's begin. A conversation read straight down is a column
/// of times with no dates in it - "09:12" over "23:40" over "08:03" is three days or one, and nothing
/// else on the screen says which.
///
/// Every instant here is built from a **local** wall-clock time, so the tests say the same thing on a
/// machine in any zone. Written with UTC times first, they claimed a message sent at 23:40 UTC on the
/// 8th belonged to the 8th - which on this Mac is half past one in the morning on the 9th, and the
/// divider was right where the test was wrong.
/// </summary>
public sealed class ChatDayDividerTests
{
    private static readonly DateTimeOffset Now = At("2026-09-10 12:00");

    [Fact]
    public void The_first_message_of_each_day_carries_its_name()
    {
        var divided = Divide(
            At("2026-09-08 09:12"), At("2026-09-08 23:40"),
            At("2026-09-09 08:03"),
            At("2026-09-10 07:15"), At("2026-09-10 11:02"));

        Assert.Equal(
            ["Tuesday", "", "Yesterday", "Today", ""],
            divided.Select(message => message.DayHeading));
    }

    /// <summary>Only the first: a divider over every message would be the times again, in words.</summary>
    [Fact]
    public void A_day_of_its_own_gets_one_divider_and_no_more()
    {
        var divided = Divide(At("2026-09-10 07:00"), At("2026-09-10 08:00"), At("2026-09-10 09:00"));

        Assert.Equal(["Today", "", ""], divided.Select(message => message.DayHeading));
        Assert.Equal([true, false, false], divided.Select(message => message.StartsANewDay));
    }

    /// <summary>
    /// Days are the reader's own local ones. Two messages an hour apart across local midnight are two
    /// days on the screen, whatever the clock the server wrote them down by says.
    /// </summary>
    [Fact]
    public void Midnight_where_the_reader_is_is_what_divides_them()
    {
        var divided = Divide(At("2026-09-09 23:30"), At("2026-09-10 00:30"));

        Assert.Equal(["Yesterday", "Today"], divided.Select(message => message.DayHeading));
    }

    [Fact]
    public void An_empty_conversation_divides_into_nothing()
        => Assert.Empty(Divide());

    /// <summary>Everything else about a message survives being divided - it is the same message.</summary>
    [Fact]
    public void Dividing_changes_nothing_but_the_heading()
    {
        var message = new ReadableChatMessage(
            IsMine: true, "Printed it already.", Now, IsEdited: true, IsWaitingToSend: false)
        {
            SentAt = "10/09/2026 12:00"
        };

        var divided = Assert.Single(ChatDays.Divide([message], Now, Translations()));

        Assert.True(divided.IsMine);
        Assert.Equal("Printed it already.", divided.Text);
        Assert.True(divided.IsEdited);
        Assert.Equal("10/09/2026 12:00", divided.SentAt);
        Assert.Equal("Today", divided.DayHeading);
    }

    private static IReadOnlyList<ReadableChatMessage> Divide(params DateTimeOffset[] sentAt)
        => ChatDays.Divide(
            [.. sentAt.Select(when => new ReadableChatMessage(
                IsMine: false, "…", when, IsEdited: false, IsWaitingToSend: false))],
            Now,
            Translations());

    /// <summary>A wall-clock time where the reader is, whatever offset that turns out to be.</summary>
    private static DateTimeOffset At(string localTime)
        => new(DateTime.Parse(localTime, CultureInfo.InvariantCulture, DateTimeStyles.None));

    private static Translations Translations() => new(new InMemoryLanguageStore());
}
