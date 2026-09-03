using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.GetCalendarEvents;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

public sealed class GetCalendarEventsQueryHandlerTests
{
    private static readonly CalendarEventDetails DefaultDetails = new(
        "Title", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false, null, [], [], ReminderNotificationChannel: NotificationChannel.None);

    private static GetCalendarEventsQueryHandler CreateHandler(
        InMemoryCalendarEventRepository calendarEventRepository, InMemoryCalendarEventShareRepository? calendarEventShareRepository = null)
        => new(new CalendarEventAccessResolver(
            calendarEventRepository, calendarEventShareRepository ?? new InMemoryCalendarEventShareRepository(), new InMemoryUserRepository()));

    [Fact]
    public async Task HandleAsync_returns_only_events_owned_by_the_requesting_user()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = CreateHandler(repository);
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
        var handler = CreateHandler(new InMemoryCalendarEventRepository());

        var events = await handler.HandleAsync(new GetCalendarEventsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(events);
    }

    [Fact]
    public async Task HandleAsync_includes_events_shared_via_an_accepted_grant_alongside_owned_events()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var calendarEventShareRepository = new InMemoryCalendarEventShareRepository();
        var handler = CreateHandler(calendarEventRepository, calendarEventShareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        await calendarEventRepository.AddAsync(CalendarEvent.Create(recipientId, DefaultDetails with { Title = "Mine" }), CancellationToken.None);
        var sharedEvent = CalendarEvent.Create(ownerId, DefaultDetails with { Title = "Shared with me" });
        await calendarEventRepository.AddAsync(sharedEvent, CancellationToken.None);
        var share = CalendarEventShare.Create(sharedEvent.Id, ownerId, recipientId);
        share.MarkAccepted();
        await calendarEventShareRepository.AddAsync(share, CancellationToken.None);

        var events = await handler.HandleAsync(new GetCalendarEventsQuery(recipientId), CancellationToken.None);

        Assert.Equal(2, events.Count);
        Assert.Contains(events, calendarEvent => calendarEvent.Details.Title == "Shared with me" && calendarEvent.IsShared);
    }
}
