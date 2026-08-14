using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.CreateCalendarEvent;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

public sealed class CreateCalendarEventCommandHandlerTests
{
    private static readonly CalendarEventDetails DefaultDetails = new(
        "Title", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false, null, [], []);

    [Fact]
    public async Task HandleAsync_creates_a_calendar_event_owned_by_the_requesting_user()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = new CreateCalendarEventCommandHandler(repository);
        var userId = Guid.NewGuid();
        var details = DefaultDetails with { Title = "Team sync" };

        var eventId = await handler.HandleAsync(new CreateCalendarEventCommand(userId, details), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, eventId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Team sync", stored!.Details.Title);
    }

    [Fact]
    public async Task HandleAsync_throws_when_the_event_ends_before_it_starts()
    {
        var handler = new CreateCalendarEventCommandHandler(new InMemoryCalendarEventRepository());
        var now = DateTimeOffset.UtcNow;
        var details = DefaultDetails with { StartUtc = now, EndUtc = now.AddHours(-1) };

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(new CreateCalendarEventCommand(Guid.NewGuid(), details), CancellationToken.None));
    }
}
