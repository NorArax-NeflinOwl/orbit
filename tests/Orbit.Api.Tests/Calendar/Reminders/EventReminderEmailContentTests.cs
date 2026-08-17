using Orbit.Core.Calendar;
using Orbit.Core.Calendar.Reminders;
using Xunit;

namespace Orbit.Api.Tests.Calendar.Reminders;

public sealed class EventReminderEmailContentTests
{
    private static readonly CalendarEventDetails DefaultDetails = new(
        "Stand-up", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30), false, null, [], [10],
        NotifyOnCreation: false, NotifyBeforeStart: true);

    [Fact]
    public void Build_includes_the_events_title_in_the_subject()
    {
        var (subject, _) = EventReminderEmailContent.Build(DefaultDetails, 10);

        Assert.Contains("Stand-up", subject);
    }

    [Fact]
    public void Build_includes_the_description_when_present()
    {
        var details = DefaultDetails with { Description = "Daily sync with the team" };

        var (_, body) = EventReminderEmailContent.Build(details, 10);

        Assert.Contains("Daily sync with the team", body);
    }

    [Fact]
    public void Build_omits_a_description_line_when_none_is_set()
    {
        var (_, body) = EventReminderEmailContent.Build(DefaultDetails, 10);

        Assert.DoesNotContain("Opis:", body);
    }

    [Fact]
    public void Build_includes_the_resolved_address_when_a_location_is_set()
    {
        var details = DefaultDetails with { Location = new EventLocation("Rynek Główny 1, Kraków", 50.0617, 19.9373) };

        var (_, body) = EventReminderEmailContent.Build(details, 10);

        Assert.Contains("Rynek Główny 1, Kraków", body);
    }

    [Fact]
    public void Build_falls_back_to_coordinates_when_the_location_has_no_address()
    {
        var details = DefaultDetails with { Location = new EventLocation(null, 50.0617, 19.9373) };

        var (_, body) = EventReminderEmailContent.Build(details, 10);

        Assert.Contains("50.06170", body);
    }
}
