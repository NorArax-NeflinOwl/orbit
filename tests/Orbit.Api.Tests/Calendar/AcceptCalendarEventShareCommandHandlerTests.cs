using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.AcceptCalendarEventShare;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

public sealed class AcceptCalendarEventShareCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_marks_the_share_accepted()
    {
        var shareRepository = new InMemoryCalendarEventShareRepository();
        var handler = new AcceptCalendarEventShareCommandHandler(shareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var share = CalendarEventShare.Create(Guid.NewGuid(), ownerId, recipientId);
        await shareRepository.AddAsync(share, CancellationToken.None);

        var accepted = await handler.HandleAsync(new AcceptCalendarEventShareCommand(recipientId, share.Id), CancellationToken.None);

        Assert.True(accepted);
        var stored = await shareRepository.GetByIdAsync(recipientId, share.Id, CancellationToken.None);
        Assert.True(stored!.IsAccepted);
    }

    [Fact]
    public async Task HandleAsync_returns_false_when_the_share_was_not_offered_to_the_requesting_user()
    {
        var shareRepository = new InMemoryCalendarEventShareRepository();
        var handler = new AcceptCalendarEventShareCommandHandler(shareRepository);
        var share = CalendarEventShare.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await shareRepository.AddAsync(share, CancellationToken.None);

        var accepted = await handler.HandleAsync(new AcceptCalendarEventShareCommand(Guid.NewGuid(), share.Id), CancellationToken.None);

        Assert.False(accepted);
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_an_unknown_share_id()
    {
        var handler = new AcceptCalendarEventShareCommandHandler(new InMemoryCalendarEventShareRepository());

        var accepted = await handler.HandleAsync(new AcceptCalendarEventShareCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(accepted);
    }

    [Fact]
    public async Task HandleAsync_is_idempotent_when_accepted_twice()
    {
        var shareRepository = new InMemoryCalendarEventShareRepository();
        var handler = new AcceptCalendarEventShareCommandHandler(shareRepository);
        var recipientId = Guid.NewGuid();
        var share = CalendarEventShare.Create(Guid.NewGuid(), Guid.NewGuid(), recipientId);
        await shareRepository.AddAsync(share, CancellationToken.None);

        var command = new AcceptCalendarEventShareCommand(recipientId, share.Id);
        await handler.HandleAsync(command, CancellationToken.None);
        var acceptedAgain = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(acceptedAgain);
    }
}
