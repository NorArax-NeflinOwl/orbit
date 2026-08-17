using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.GetCalendarEventById;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

public sealed class GetCalendarEventByIdQueryHandlerTests
{
    private static readonly CalendarEventDetails DefaultDetails = new(
        "Title", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false, null, [], [],
        NotifyOnCreation: false, NotifyBeforeStart: false);

    [Fact]
    public async Task HandleAsync_returns_the_event_when_owned_by_the_requesting_user()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = new GetCalendarEventByIdQueryHandler(repository);
        var userId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(userId, DefaultDetails);
        await repository.AddAsync(calendarEvent, CancellationToken.None);

        var result = await handler.HandleAsync(new GetCalendarEventByIdQuery(userId, calendarEvent.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(calendarEvent.Id, result!.Id);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_event_owned_by_a_different_user()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = new GetCalendarEventByIdQueryHandler(repository);
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
        var handler = new GetCalendarEventByIdQueryHandler(new InMemoryCalendarEventRepository());

        var result = await handler.HandleAsync(new GetCalendarEventByIdQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}
