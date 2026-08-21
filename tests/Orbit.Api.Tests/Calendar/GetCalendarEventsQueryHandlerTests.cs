using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.GetCalendarEvents;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

public sealed class GetCalendarEventsQueryHandlerTests
{
    private static readonly CalendarEventDetails DefaultDetails = new(
        "Title", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false, null, [], [],
        CreationNotificationChannel: NotificationChannel.None, ReminderNotificationChannel: NotificationChannel.None);

    [Fact]
    public async Task HandleAsync_returns_only_events_owned_by_the_requesting_user()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = new GetCalendarEventsQueryHandler(repository);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await repository.AddAsync(CalendarEvent.Create(userId, DefaultDetails with { Title = "Mine" }), CancellationToken.None);
        await repository.AddAsync(CalendarEvent.Create(otherUserId, DefaultDetails with { Title = "Not mine" }), CancellationToken.None);

        var events = await handler.HandleAsync(new GetCalendarEventsQuery(userId), CancellationToken.None);

        var calendarEvent = Assert.Single(events);
        Assert.Equal("Mine", calendarEvent.Details.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_an_empty_list_when_the_user_has_no_events()
    {
        var handler = new GetCalendarEventsQueryHandler(new InMemoryCalendarEventRepository());

        var events = await handler.HandleAsync(new GetCalendarEventsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(events);
    }
}
