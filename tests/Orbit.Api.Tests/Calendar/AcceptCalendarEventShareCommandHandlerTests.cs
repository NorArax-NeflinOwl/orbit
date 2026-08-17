using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.AcceptCalendarEventShare;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

public sealed class AcceptCalendarEventShareCommandHandlerTests
{
    private static readonly CalendarEventDetails DefaultDetails = new(
        "Team sync", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false, null, [], [],
        NotifyOnCreation: false, NotifyBeforeStart: false);

    [Fact]
    public async Task HandleAsync_creates_a_read_only_copy_in_the_recipients_calendar()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var shareRepository = new InMemoryCalendarEventShareRepository();
        var userRepository = new InMemoryUserRepository();
        var handler = new AcceptCalendarEventShareCommandHandler(shareRepository, calendarEventRepository, userRepository);

        var owner = User.Create("owner@example.com", "owner", "Owner", "hash");
        await userRepository.AddAsync(owner, CancellationToken.None);
        var recipientId = Guid.NewGuid();
        var sourceEvent = CalendarEvent.Create(owner.Id, DefaultDetails);
        await calendarEventRepository.AddAsync(sourceEvent, CancellationToken.None);
        var share = CalendarEventShare.Create(sourceEvent.Id, owner.Id, recipientId);
        await shareRepository.AddAsync(share, CancellationToken.None);

        var accepted = await handler.HandleAsync(new AcceptCalendarEventShareCommand(recipientId, share.Id), CancellationToken.None);

        Assert.True(accepted);
        var recipientEvents = await calendarEventRepository.GetAllAsync(recipientId, CancellationToken.None);
        var sharedCopy = Assert.Single(recipientEvents);
        Assert.True(sharedCopy.IsShared);
        Assert.Equal("owner", sharedCopy.SharedByUserName);
        Assert.Equal("Team sync", sharedCopy.Details.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_false_when_the_share_was_not_offered_to_the_requesting_user()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var shareRepository = new InMemoryCalendarEventShareRepository();
        var userRepository = new InMemoryUserRepository();
        var handler = new AcceptCalendarEventShareCommandHandler(shareRepository, calendarEventRepository, userRepository);

        var owner = User.Create("owner@example.com", "owner", "Owner", "hash");
        await userRepository.AddAsync(owner, CancellationToken.None);
        var sourceEvent = CalendarEvent.Create(owner.Id, DefaultDetails);
        await calendarEventRepository.AddAsync(sourceEvent, CancellationToken.None);
        var share = CalendarEventShare.Create(sourceEvent.Id, owner.Id, Guid.NewGuid());
        await shareRepository.AddAsync(share, CancellationToken.None);

        var accepted = await handler.HandleAsync(
            new AcceptCalendarEventShareCommand(Guid.NewGuid(), share.Id), CancellationToken.None);

        Assert.False(accepted);
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_an_unknown_share_id()
    {
        var handler = new AcceptCalendarEventShareCommandHandler(
            new InMemoryCalendarEventShareRepository(), new InMemoryCalendarEventRepository(), new InMemoryUserRepository());

        var accepted = await handler.HandleAsync(
            new AcceptCalendarEventShareCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(accepted);
    }

    [Fact]
    public async Task HandleAsync_is_idempotent_and_does_not_create_a_second_copy_when_accepted_twice()
    {
        var calendarEventRepository = new InMemoryCalendarEventRepository();
        var shareRepository = new InMemoryCalendarEventShareRepository();
        var userRepository = new InMemoryUserRepository();
        var handler = new AcceptCalendarEventShareCommandHandler(shareRepository, calendarEventRepository, userRepository);

        var owner = User.Create("owner@example.com", "owner", "Owner", "hash");
        await userRepository.AddAsync(owner, CancellationToken.None);
        var recipientId = Guid.NewGuid();
        var sourceEvent = CalendarEvent.Create(owner.Id, DefaultDetails);
        await calendarEventRepository.AddAsync(sourceEvent, CancellationToken.None);
        var share = CalendarEventShare.Create(sourceEvent.Id, owner.Id, recipientId);
        await shareRepository.AddAsync(share, CancellationToken.None);

        var command = new AcceptCalendarEventShareCommand(recipientId, share.Id);
        await handler.HandleAsync(command, CancellationToken.None);
        var acceptedAgain = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(acceptedAgain);
        var recipientEvents = await calendarEventRepository.GetAllAsync(recipientId, CancellationToken.None);
        Assert.Single(recipientEvents);
    }
}
