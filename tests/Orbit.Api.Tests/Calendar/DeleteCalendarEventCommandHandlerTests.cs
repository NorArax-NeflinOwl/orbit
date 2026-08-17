using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.DeleteCalendarEvent;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

public sealed class DeleteCalendarEventCommandHandlerTests
{
    private static readonly CalendarEventDetails DefaultDetails = new(
        "Title", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false, null, [], [],
        NotifyOnCreation: false, NotifyBeforeStart: false);

    [Fact]
    public async Task HandleAsync_deletes_an_event_owned_by_the_requesting_user()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = new DeleteCalendarEventCommandHandler(repository);
        var userId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(userId, DefaultDetails);
        await repository.AddAsync(calendarEvent, CancellationToken.None);

        var wasDeleted = await handler.HandleAsync(new DeleteCalendarEventCommand(userId, calendarEvent.Id), CancellationToken.None);

        Assert.True(wasDeleted);
        Assert.Null(await repository.GetByIdAsync(userId, calendarEvent.Id, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_returns_false_and_does_not_delete_an_event_owned_by_a_different_user()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = new DeleteCalendarEventCommandHandler(repository);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails);
        await repository.AddAsync(calendarEvent, CancellationToken.None);

        var wasDeleted = await handler.HandleAsync(new DeleteCalendarEventCommand(otherUserId, calendarEvent.Id), CancellationToken.None);

        Assert.False(wasDeleted);
        Assert.NotNull(await repository.GetByIdAsync(ownerId, calendarEvent.Id, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_an_unknown_event_id()
    {
        var handler = new DeleteCalendarEventCommandHandler(new InMemoryCalendarEventRepository());

        var wasDeleted = await handler.HandleAsync(new DeleteCalendarEventCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(wasDeleted);
    }
}
