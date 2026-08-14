using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.UpdateCalendarEvent;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

public sealed class UpdateCalendarEventCommandHandlerTests
{
    private static readonly CalendarEventDetails DefaultDetails = new(
        "Title", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false, null, [], []);

    [Fact]
    public async Task HandleAsync_updates_an_event_owned_by_the_requesting_user()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = new UpdateCalendarEventCommandHandler(repository);
        var userId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(userId, DefaultDetails with { Title = "Original title" });
        await repository.AddAsync(calendarEvent, CancellationToken.None);
        var newDetails = DefaultDetails with { Title = "New title" };

        var wasUpdated = await handler.HandleAsync(
            new UpdateCalendarEventCommand(userId, calendarEvent.Id, newDetails), CancellationToken.None);

        Assert.True(wasUpdated);
        var stored = await repository.GetByIdAsync(userId, calendarEvent.Id, CancellationToken.None);
        Assert.Equal("New title", stored!.Details.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_false_and_does_not_update_an_event_owned_by_a_different_user()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = new UpdateCalendarEventCommandHandler(repository);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails with { Title = "Original title" });
        await repository.AddAsync(calendarEvent, CancellationToken.None);

        var wasUpdated = await handler.HandleAsync(
            new UpdateCalendarEventCommand(otherUserId, calendarEvent.Id, DefaultDetails with { Title = "Hijacked title" }), CancellationToken.None);

        Assert.False(wasUpdated);
        var stored = await repository.GetByIdAsync(ownerId, calendarEvent.Id, CancellationToken.None);
        Assert.Equal("Original title", stored!.Details.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_an_unknown_event_id()
    {
        var handler = new UpdateCalendarEventCommandHandler(new InMemoryCalendarEventRepository());

        var wasUpdated = await handler.HandleAsync(
            new UpdateCalendarEventCommand(Guid.NewGuid(), Guid.NewGuid(), DefaultDetails), CancellationToken.None);

        Assert.False(wasUpdated);
    }
}
