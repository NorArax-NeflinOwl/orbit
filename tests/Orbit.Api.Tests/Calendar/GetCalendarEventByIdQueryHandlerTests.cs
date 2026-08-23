using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.GetCalendarEventById;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

public sealed class GetCalendarEventByIdQueryHandlerTests
{
    private static readonly CalendarEventDetails DefaultDetails = new(
        "Title", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false, null, [], [],
        CreationNotificationChannel: NotificationChannel.None, ReminderNotificationChannel: NotificationChannel.None);

    private static GetCalendarEventByIdQueryHandler CreateHandler(
        InMemoryCalendarEventRepository calendarEventRepository, InMemoryCalendarEventShareRepository? calendarEventShareRepository = null)
        => new(new CalendarEventAccessResolver(
            calendarEventRepository, calendarEventShareRepository ?? new InMemoryCalendarEventShareRepository(), new InMemoryUserRepository()));

    [Fact]
    public async Task HandleAsync_returns_the_event_when_owned_by_the_requesting_user()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(userId, DefaultDetails);
        await repository.AddAsync(calendarEvent, CancellationToken.None);

        var result = await handler.HandleAsync(new GetCalendarEventByIdQuery(userId, calendarEvent.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(calendarEvent.Id, result!.Id);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_event_neither_owned_by_nor_shared_with_the_requesting_user()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = CreateHandler(repository);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails);
        await repository.AddAsync(calendarEvent, CancellationToken.None);

        var result = await handler.HandleAsync(new GetCalendarEventByIdQuery(otherUserId, calendarEvent.Id), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_unknown_event_id()
    {
        var handler = CreateHandler(new InMemoryCalendarEventRepository());

        var result = await handler.HandleAsync(new GetCalendarEventByIdQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_returns_the_event_with_access_context_when_shared_via_an_accepted_grant()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var calendarEventShareRepository = new InMemoryCalendarEventShareRepository();
        var handler = CreateHandler(calendarEventRepository, calendarEventShareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails);
        await calendarEventRepository.AddAsync(calendarEvent, CancellationToken.None);
        var share = CalendarEventShare.Create(calendarEvent.Id, ownerId, recipientId, ShareAccessLevel.ReadOnly);
        share.MarkAccepted();
        await calendarEventShareRepository.AddAsync(share, CancellationToken.None);

        var result = await handler.HandleAsync(new GetCalendarEventByIdQuery(recipientId, calendarEvent.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsShared);
        Assert.Equal(ShareAccessLevel.ReadOnly, result.AccessLevel);
    }
}
