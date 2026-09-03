using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.CreateCalendarEvent;
using Orbit.Core.LiveUpdates;
using Orbit.Core.Notifications;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

public sealed class CreateCalendarEventCommandHandlerTests
{
    private static readonly CalendarEventDetails DefaultDetails = new(
        "Title", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false, null, [], [], ReminderNotificationChannel: NotificationChannel.None);

    [Fact]
    public async Task HandleAsync_creates_a_calendar_event_owned_by_the_requesting_user()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = CreateHandler(repository);
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
        var handler = CreateHandler(new InMemoryCalendarEventRepository());
        var now = DateTimeOffset.UtcNow;
        var details = DefaultDetails with { StartUtc = now, EndUtc = now.AddHours(-1) };

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => handler.HandleAsync(new CreateCalendarEventCommand(Guid.NewGuid(), details), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_creates_a_calendar_event_with_a_map_location()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var location = new EventLocation("Rynek Główny 1, Kraków", 50.0617, 19.9373);
        var details = DefaultDetails with { Location = location };

        var eventId = await handler.HandleAsync(new CreateCalendarEventCommand(userId, details), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, eventId, CancellationToken.None);
        Assert.Equal(location, stored!.Details.Location);
    }

    [Theory]
    [InlineData(90.1, 19.9373)]
    [InlineData(-90.1, 19.9373)]
    [InlineData(50.0617, 180.1)]
    [InlineData(50.0617, -180.1)]
    public async Task HandleAsync_throws_when_the_locations_coordinates_are_out_of_range(double latitude, double longitude)
    {
        var handler = CreateHandler(new InMemoryCalendarEventRepository());
        var details = DefaultDetails with { Location = new EventLocation(null, latitude, longitude) };

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => handler.HandleAsync(new CreateCalendarEventCommand(Guid.NewGuid(), details), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_rejects_an_out_of_range_location_with_a_message_fit_to_show_the_caller()
    {
        var handler = CreateHandler(new InMemoryCalendarEventRepository());
        // The longitude a map click reports after panning two worlds east - what actually reached the
        // API before mapPicker.js started wrapping the picked point.
        var details = DefaultDetails with { Location = new EventLocation(null, 50.0617, 254.09) };

        var exception = await Assert.ThrowsAsync<InvalidRequestException>(
            () => handler.HandleAsync(new CreateCalendarEventCommand(Guid.NewGuid(), details), CancellationToken.None));

        // Returned verbatim as the 400 body (see CalendarEndpoints.ToValidationFailure), so it must read
        // as a sentence rather than carrying .NET's "(Parameter 'details')" suffix.
        Assert.Equal("A location's longitude must be between -180 and 180 degrees.", exception.Message);
    }

    /// <summary>
    /// The three tests about emailing and pushing "you made an event" went with the feature: an event
    /// no longer announces itself to the person who just made it. What is said when something is saved
    /// is said to somebody else - see ShareCalendarEventCommandHandlerTests.
    /// </summary>
    private static CreateCalendarEventCommandHandler CreateHandler(InMemoryCalendarEventRepository repository)
        => new(repository);
}
