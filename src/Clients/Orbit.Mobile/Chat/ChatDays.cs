using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;

namespace Orbit.Mobile.Chat;

/// <summary>
/// Where one day's messages end and the next day's begin, so a thread can say so.
///
/// A conversation read straight down is a column of times with no dates in it: "09:12" over "23:40"
/// over "08:03" is three days or one, and nothing on the screen says which. The design puts a small
/// centred word between the runs, and this is what decides where those words go - the first message of
/// each day carries the name of it and the rest carry nothing, so the thread needs no grouping and the
/// row template needs no notion of what came before it.
///
/// Days are the reader's own local ones, not UTC: a message sent at 00:30 in Warsaw belongs to the day
/// the reader saw it, and grouping by UTC would put it under the day before for half the year.
/// </summary>
public static class ChatDays
{
    /// <param name="nowUtc">
    /// Taken rather than read off the machine, for the same reason <see cref="LastChanged"/> takes it:
    /// "today" is whatever the injected clock says, which is what lets a test about the wording be a
    /// test about the wording.
    /// </param>
    public static IReadOnlyList<ReadableChatMessage> Divide(
        IReadOnlyList<ReadableChatMessage> conversation, DateTimeOffset nowUtc, Translations translations)
    {
        var divided = new List<ReadableChatMessage>(conversation.Count);
        DateTime? dayBefore = null;

        foreach (var message in conversation)
        {
            var sentOn = message.SentAtUtc.ToLocalTime().Date;

            divided.Add(sentOn == dayBefore
                ? message
                : message with { DayHeading = LastChanged.Describe(message.SentAtUtc, nowUtc, translations) });

            dayBefore = sentOn;
        }

        return divided;
    }
}
