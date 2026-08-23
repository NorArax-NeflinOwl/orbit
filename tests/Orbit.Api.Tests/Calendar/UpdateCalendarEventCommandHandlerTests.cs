using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.UpdateCalendarEvent;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

public sealed class UpdateCalendarEventCommandHandlerTests
{
    private static readonly CalendarEventDetails DefaultDetails = new(
        "Title", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false, null, [], [],
        CreationNotificationChannel: NotificationChannel.None, ReminderNotificationChannel: NotificationChannel.None);

    private static UpdateCalendarEventCommandHandler CreateHandler(
        InMemoryCalendarEventRepository calendarEventRepository, InMemoryCalendarEventShareRepository? calendarEventShareRepository = null)
        => new(
            new CalendarEventAccessResolver(
                calendarEventRepository, calendarEventShareRepository ?? new InMemoryCalendarEventShareRepository(), new InMemoryUserRepository()),
            calendarEventRepository);

    [Fact]
    public async Task HandleAsync_updates_an_event_owned_by_the_requesting_user()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(userId, DefaultDetails with { Title = "Original title" });
        await repository.AddAsync(calendarEvent, CancellationToken.None);
        var newDetails = DefaultDetails with { Title = "New title" };

        var outcome = await handler.HandleAsync(new UpdateCalendarEventCommand(userId, calendarEvent.Id, newDetails), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        var stored = await repository.GetByIdAsync(userId, calendarEvent.Id, CancellationToken.None);
        Assert.Equal("New title", stored!.Details.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_NotFound_and_does_not_update_an_event_owned_by_a_different_user()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = CreateHandler(repository);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails with { Title = "Original title" });
        await repository.AddAsync(calendarEvent, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new UpdateCalendarEventCommand(otherUserId, calendarEvent.Id, DefaultDetails with { Title = "Hijacked title" }), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
        var stored = await repository.GetByIdAsync(ownerId, calendarEvent.Id, CancellationToken.None);
        Assert.Equal("Original title", stored!.Details.Title);
    }

    private static async Task<(InMemoryCalendarEventRepository CalendarEventRepository, Guid OwnerId, Guid RecipientId, CalendarEvent CalendarEvent)>
        CreateSharedEventAsync(InMemoryCalendarEventShareRepository calendarEventShareRepository, ShareAccessLevel accessLevel)
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails with { Title = "Original title" });
        await calendarEventRepository.AddAsync(calendarEvent, CancellationToken.None);
        var share = CalendarEventShare.Create(calendarEvent.Id, ownerId, recipientId, accessLevel);
        share.MarkAccepted();
        await calendarEventShareRepository.AddAsync(share, CancellationToken.None);
        return (calendarEventRepository, ownerId, recipientId, calendarEvent);
    }

    [Fact]
    public async Task HandleAsync_returns_NotFound_and_does_not_update_a_shared_read_only_event()
    {
        var calendarEventShareRepository = new InMemoryCalendarEventShareRepository();
        var (calendarEventRepository, _, recipientId, calendarEvent) =
            await CreateSharedEventAsync(calendarEventShareRepository, ShareAccessLevel.ReadOnly);
        var handler = CreateHandler(calendarEventRepository, calendarEventShareRepository);

        var outcome = await handler.HandleAsync(
            new UpdateCalendarEventCommand(recipientId, calendarEvent.Id, DefaultDetails with { Title = "Edited title" }), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
    }

    [Fact]
    public async Task HandleAsync_returns_NotFound_and_does_not_update_an_event_shared_at_the_Share_tier()
    {
        var calendarEventShareRepository = new InMemoryCalendarEventShareRepository();
        var (calendarEventRepository, _, recipientId, calendarEvent) =
            await CreateSharedEventAsync(calendarEventShareRepository, ShareAccessLevel.Share);
        var handler = CreateHandler(calendarEventRepository, calendarEventShareRepository);

        var outcome = await handler.HandleAsync(
            new UpdateCalendarEventCommand(recipientId, calendarEvent.Id, DefaultDetails with { Title = "Edited title" }), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
    }

    [Fact]
    public async Task HandleAsync_updates_a_shared_event_with_edit_access()
    {
        var calendarEventShareRepository = new InMemoryCalendarEventShareRepository();
        var (calendarEventRepository, ownerId, recipientId, calendarEvent) =
            await CreateSharedEventAsync(calendarEventShareRepository, ShareAccessLevel.CanEdit);
        var handler = CreateHandler(calendarEventRepository, calendarEventShareRepository);

        var outcome = await handler.HandleAsync(
            new UpdateCalendarEventCommand(recipientId, calendarEvent.Id, DefaultDetails with { Title = "New title" }), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        var stored = await calendarEventRepository.GetByIdAsync(ownerId, calendarEvent.Id, CancellationToken.None);
        Assert.Equal("New title", stored!.Details.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_Locked_when_someone_else_holds_the_edit_lock()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(userId, DefaultDetails);
        calendarEvent.AcquireLock(otherUserId, "otherUser", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        await repository.AddAsync(calendarEvent, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new UpdateCalendarEventCommand(userId, calendarEvent.Id, DefaultDetails with { Title = "Edited title" }), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Locked, outcome.Kind);
        Assert.Equal("otherUser", outcome.LockedByUserName);
    }

    [Fact]
    public async Task HandleAsync_returns_NotFound_for_an_unknown_event_id()
    {
        var handler = CreateHandler(new InMemoryCalendarEventRepository());

        var outcome = await handler.HandleAsync(new UpdateCalendarEventCommand(Guid.NewGuid(), Guid.NewGuid(), DefaultDetails), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
    }

    [Fact]
    public async Task HandleAsync_updates_an_events_location()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(userId, DefaultDetails);
        await repository.AddAsync(calendarEvent, CancellationToken.None);
        var location = new EventLocation("Rynek Główny 1, Kraków", 50.0617, 19.9373);

        await handler.HandleAsync(new UpdateCalendarEventCommand(userId, calendarEvent.Id, DefaultDetails with { Location = location }), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, calendarEvent.Id, CancellationToken.None);
        Assert.Equal(location, stored!.Details.Location);
    }

    [Fact]
    public async Task HandleAsync_throws_when_the_updated_locations_latitude_is_out_of_range()
    {
        var repository = new InMemoryCalendarEventRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(userId, DefaultDetails);
        await repository.AddAsync(calendarEvent, CancellationToken.None);
        var invalidLocation = new EventLocation(null, 90.1, 19.9373);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(
            new UpdateCalendarEventCommand(userId, calendarEvent.Id, DefaultDetails with { Location = invalidLocation }), CancellationToken.None));
    }
}
