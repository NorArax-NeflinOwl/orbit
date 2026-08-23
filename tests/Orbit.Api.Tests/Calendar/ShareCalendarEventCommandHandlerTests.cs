using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.ShareCalendarEvent;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

public sealed class ShareCalendarEventCommandHandlerTests
{
    private static readonly CalendarEventDetails DefaultDetails = new(
        "Title", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false, null, [], [],
        CreationNotificationChannel: NotificationChannel.None, ReminderNotificationChannel: NotificationChannel.None);

    [Fact]
    public async Task HandleAsync_creates_a_share_for_an_event_owned_by_the_requesting_user()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var shareRepository = new InMemoryCalendarEventShareRepository();
        var handler = new ShareCalendarEventCommandHandler(calendarEventRepository, shareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails);
        await calendarEventRepository.AddAsync(calendarEvent, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new ShareCalendarEventCommand(ownerId, calendarEvent.Id, recipientId), CancellationToken.None);

        Assert.NotNull(outcome);
        Assert.False(outcome!.AlreadyShared);
        var share = await shareRepository.GetByIdAsync(recipientId, outcome.ShareId, CancellationToken.None);
        Assert.NotNull(share);
        Assert.Equal(calendarEvent.Id, share!.SourceCalendarEventId);
        Assert.Equal(ownerId, share.OwnerUserId);
        Assert.Equal(recipientId, share.RecipientUserId);
        Assert.Equal(ownerId, share.OriginalOwnerUserId);
        Assert.False(share.IsAccepted);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_event_not_owned_by_the_requesting_user()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var handler = new ShareCalendarEventCommandHandler(calendarEventRepository, new InMemoryCalendarEventShareRepository());
        var ownerId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails);
        await calendarEventRepository.AddAsync(calendarEvent, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new ShareCalendarEventCommand(Guid.NewGuid(), calendarEvent.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_unknown_event_id()
    {
        var handler = new ShareCalendarEventCommandHandler(new InMemoryCalendarEventRepository(), new InMemoryCalendarEventShareRepository());

        var outcome = await handler.HandleAsync(
            new ShareCalendarEventCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_sharing_back_to_the_original_owner()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var handler = new ShareCalendarEventCommandHandler(calendarEventRepository, new InMemoryCalendarEventShareRepository());
        var ownerId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails);
        await calendarEventRepository.AddAsync(calendarEvent, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new ShareCalendarEventCommand(ownerId, calendarEvent.Id, ownerId), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_a_ReadOnly_recipient_tries_to_re_share()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var handler = new ShareCalendarEventCommandHandler(calendarEventRepository, new InMemoryCalendarEventShareRepository());
        var originalOwnerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var sharedCopy = CalendarEvent.CreateShared(recipientId, DefaultDetails, "owner", ShareAccessLevel.ReadOnly, originalOwnerId);
        await calendarEventRepository.AddAsync(sharedCopy, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new ShareCalendarEventCommand(recipientId, sharedCopy.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_lets_a_Share_tier_recipient_re_share_at_ReadOnly_or_Share_but_not_CanEdit()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var handler = new ShareCalendarEventCommandHandler(calendarEventRepository, new InMemoryCalendarEventShareRepository());
        var originalOwnerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var sharedCopy = CalendarEvent.CreateShared(recipientId, DefaultDetails, "owner", ShareAccessLevel.Share, originalOwnerId);
        await calendarEventRepository.AddAsync(sharedCopy, CancellationToken.None);

        var shareOutcome = await handler.HandleAsync(
            new ShareCalendarEventCommand(recipientId, sharedCopy.Id, Guid.NewGuid(), ShareAccessLevel.Share), CancellationToken.None);
        var canEditOutcome = await handler.HandleAsync(
            new ShareCalendarEventCommand(recipientId, sharedCopy.Id, Guid.NewGuid(), ShareAccessLevel.CanEdit), CancellationToken.None);

        Assert.NotNull(shareOutcome);
        Assert.Null(canEditOutcome);
    }

    [Fact]
    public async Task HandleAsync_reuses_an_existing_offer_to_the_same_recipient_instead_of_creating_a_duplicate()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var shareRepository = new InMemoryCalendarEventShareRepository();
        var handler = new ShareCalendarEventCommandHandler(calendarEventRepository, shareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails);
        await calendarEventRepository.AddAsync(calendarEvent, CancellationToken.None);

        var firstOutcome = await handler.HandleAsync(
            new ShareCalendarEventCommand(ownerId, calendarEvent.Id, recipientId), CancellationToken.None);
        var secondOutcome = await handler.HandleAsync(
            new ShareCalendarEventCommand(ownerId, calendarEvent.Id, recipientId), CancellationToken.None);

        Assert.NotNull(firstOutcome);
        Assert.False(firstOutcome!.AlreadyShared);
        Assert.NotNull(secondOutcome);
        Assert.True(secondOutcome!.AlreadyShared);
        Assert.Equal(firstOutcome.ShareId, secondOutcome.ShareId);
    }
}
