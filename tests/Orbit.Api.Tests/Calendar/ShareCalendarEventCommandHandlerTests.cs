using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.ShareCalendarEvent;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

public sealed class ShareCalendarEventCommandHandlerTests
{
    private static readonly CalendarEventDetails DefaultDetails = new(
        "Title", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false, null, [], [],
        NotifyOnCreation: false, NotifyBeforeStart: false);

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

        var shareId = await handler.HandleAsync(
            new ShareCalendarEventCommand(ownerId, calendarEvent.Id, recipientId), CancellationToken.None);

        Assert.NotNull(shareId);
        var share = await shareRepository.GetByIdAsync(recipientId, shareId!.Value, CancellationToken.None);
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
        var handler = new ShareCalendarEventCommandHandler(calendarEventRepository, new InMemoryCalendarEventShareRepository());
        var ownerId = Guid.NewGuid();
        var calendarEvent = CalendarEvent.Create(ownerId, DefaultDetails);
        await calendarEventRepository.AddAsync(calendarEvent, CancellationToken.None);

        var shareId = await handler.HandleAsync(
            new ShareCalendarEventCommand(Guid.NewGuid(), calendarEvent.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Null(shareId);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_unknown_event_id()
    {
        var handler = new ShareCalendarEventCommandHandler(new InMemoryCalendarEventRepository(), new InMemoryCalendarEventShareRepository());

        var shareId = await handler.HandleAsync(
            new ShareCalendarEventCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(shareId);
    }
}
