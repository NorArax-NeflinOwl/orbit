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
        "Title", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false, null, [], [], ReminderNotificationChannel: NotificationChannel.None);

    private static ShareCalendarEventCommandHandler CreateHandler(
        InMemoryCalendarEventRepository calendarEventRepository, InMemoryCalendarEventShareRepository calendarEventShareRepository,
        RecordingSharedItemNotifier? sharedItemNotifier = null)
        => new(
            new CalendarEventAccessResolver(calendarEventRepository, calendarEventShareRepository, new InMemoryUserRepository()),
            calendarEventShareRepository, sharedItemNotifier ?? new RecordingSharedItemNotifier());

    [Fact]
    public async Task HandleAsync_invites_the_guest_it_shared_the_event_with()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var sharedItemNotifier = new RecordingSharedItemNotifier();
        var handler = CreateHandler(calendarEventRepository, new InMemoryCalendarEventShareRepository(), sharedItemNotifier);
        var ownerId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails with { Title = "Dentist" });
        await calendarEventRepository.AddAsync(calendarEvent, CancellationToken.None);

        await handler.HandleAsync(new ShareCalendarEventCommand(ownerId, calendarEvent.Id, guestId), CancellationToken.None);

        // A guest used to find out about an event only by opening Orbit and noticing it. The invitation
        // is what makes being added to someone's event something you hear about.
        var announcement = Assert.Single(sharedItemNotifier.Announced);
        Assert.Equal(guestId, announcement.RecipientUserId);
        Assert.Equal(ownerId, announcement.SharerUserId);
        Assert.Equal(SharedItemKind.CalendarEvent, announcement.Kind);
        Assert.Equal("Dentist", announcement.ItemTitle);
    }

    [Fact]
    public async Task HandleAsync_does_not_invite_a_guest_who_was_already_invited()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var sharedItemNotifier = new RecordingSharedItemNotifier();
        var handler = CreateHandler(calendarEventRepository, new InMemoryCalendarEventShareRepository(), sharedItemNotifier);
        var ownerId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails);
        await calendarEventRepository.AddAsync(calendarEvent, CancellationToken.None);
        await handler.HandleAsync(new ShareCalendarEventCommand(ownerId, calendarEvent.Id, guestId), CancellationToken.None);

        await handler.HandleAsync(new ShareCalendarEventCommand(ownerId, calendarEvent.Id, guestId), CancellationToken.None);

        Assert.Single(sharedItemNotifier.Announced);
    }

    [Fact]
    public async Task HandleAsync_creates_a_share_for_an_event_owned_by_the_requesting_user()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var shareRepository = new InMemoryCalendarEventShareRepository();
        var handler = CreateHandler(calendarEventRepository, shareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails);
        await calendarEventRepository.AddAsync(calendarEvent, CancellationToken.None);

        var outcome = await handler.HandleAsync(new ShareCalendarEventCommand(ownerId, calendarEvent.Id, recipientId), CancellationToken.None);

        Assert.NotNull(outcome);
        Assert.False(outcome!.AlreadyShared);
        var share = await shareRepository.GetByIdAsync(recipientId, outcome.ShareId, CancellationToken.None);
        Assert.NotNull(share);
        Assert.Equal(calendarEvent.Id, share!.SourceCalendarEventId);
        Assert.Equal(ownerId, share.OwnerUserId);
        Assert.Equal(recipientId, share.RecipientUserId);
        Assert.False(share.IsAccepted);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_event_not_owned_by_the_requesting_user()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var handler = CreateHandler(calendarEventRepository, new InMemoryCalendarEventShareRepository());
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
        var handler = CreateHandler(new InMemoryCalendarEventRepository(), new InMemoryCalendarEventShareRepository());

        var outcome = await handler.HandleAsync(
            new ShareCalendarEventCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_sharing_back_to_the_owner()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var handler = CreateHandler(calendarEventRepository, new InMemoryCalendarEventShareRepository());
        var ownerId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails);
        await calendarEventRepository.AddAsync(calendarEvent, CancellationToken.None);

        var outcome = await handler.HandleAsync(new ShareCalendarEventCommand(ownerId, calendarEvent.Id, ownerId), CancellationToken.None);

        Assert.Null(outcome);
    }

    private static async Task<(Guid OwnerId, Guid RecipientId, CalendarEvent CalendarEvent)> ShareEventWithAcceptedGrantAsync(
        InMemoryCalendarEventRepository calendarEventRepository, InMemoryCalendarEventShareRepository calendarEventShareRepository,
        ShareAccessLevel accessLevel)
    {
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails);
        await calendarEventRepository.AddAsync(calendarEvent, CancellationToken.None);
        var grant = CalendarEventShare.Create(calendarEvent.Id, ownerId, recipientId, accessLevel);
        grant.MarkAccepted();
        await calendarEventShareRepository.AddAsync(grant, CancellationToken.None);
        return (ownerId, recipientId, calendarEvent);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_a_ReadOnly_recipient_tries_to_re_share()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var calendarEventShareRepository = new InMemoryCalendarEventShareRepository();
        var (_, recipientId, calendarEvent) =
            await ShareEventWithAcceptedGrantAsync(calendarEventRepository, calendarEventShareRepository, ShareAccessLevel.ReadOnly);
        var handler = CreateHandler(calendarEventRepository, calendarEventShareRepository);

        var outcome = await handler.HandleAsync(new ShareCalendarEventCommand(recipientId, calendarEvent.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_lets_a_Share_tier_recipient_re_share_at_ReadOnly_or_Share_but_not_CanEdit()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var calendarEventShareRepository = new InMemoryCalendarEventShareRepository();
        var (_, recipientId, calendarEvent) =
            await ShareEventWithAcceptedGrantAsync(calendarEventRepository, calendarEventShareRepository, ShareAccessLevel.Share);
        var handler = CreateHandler(calendarEventRepository, calendarEventShareRepository);

        var shareOutcome = await handler.HandleAsync(
            new ShareCalendarEventCommand(recipientId, calendarEvent.Id, Guid.NewGuid(), ShareAccessLevel.Share), CancellationToken.None);
        var canEditOutcome = await handler.HandleAsync(
            new ShareCalendarEventCommand(recipientId, calendarEvent.Id, Guid.NewGuid(), ShareAccessLevel.CanEdit), CancellationToken.None);

        Assert.NotNull(shareOutcome);
        Assert.Null(canEditOutcome);
    }

    [Fact]
    public async Task HandleAsync_reuses_an_existing_offer_to_the_same_recipient_instead_of_creating_a_duplicate()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var shareRepository = new InMemoryCalendarEventShareRepository();
        var handler = CreateHandler(calendarEventRepository, shareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails);
        await calendarEventRepository.AddAsync(calendarEvent, CancellationToken.None);

        var firstOutcome = await handler.HandleAsync(new ShareCalendarEventCommand(ownerId, calendarEvent.Id, recipientId), CancellationToken.None);
        var secondOutcome = await handler.HandleAsync(new ShareCalendarEventCommand(ownerId, calendarEvent.Id, recipientId), CancellationToken.None);

        Assert.NotNull(firstOutcome);
        Assert.False(firstOutcome!.AlreadyShared);
        Assert.NotNull(secondOutcome);
        Assert.True(secondOutcome!.AlreadyShared);
        Assert.Equal(firstOutcome.ShareId, secondOutcome.ShareId);
    }
}
