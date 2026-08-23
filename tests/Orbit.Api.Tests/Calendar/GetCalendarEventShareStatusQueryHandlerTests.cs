using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.GetCalendarEventShareStatus;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

public sealed class GetCalendarEventShareStatusQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_false_for_a_pending_share()
    {
        var repository = new InMemoryCalendarEventShareRepository();
        var handler = new GetCalendarEventShareStatusQueryHandler(repository);
        var recipientId = Guid.NewGuid();
        var share = CalendarEventShare.Create(Guid.NewGuid(), Guid.NewGuid(), recipientId);
        await repository.AddAsync(share, CancellationToken.None);

        var isAccepted = await handler.HandleAsync(new GetCalendarEventShareStatusQuery(recipientId, share.Id), CancellationToken.None);

        Assert.False(isAccepted);
    }

    [Fact]
    public async Task HandleAsync_returns_true_once_the_share_has_been_accepted()
    {
        var repository = new InMemoryCalendarEventShareRepository();
        var handler = new GetCalendarEventShareStatusQueryHandler(repository);
        var recipientId = Guid.NewGuid();
        var share = CalendarEventShare.Create(Guid.NewGuid(), Guid.NewGuid(), recipientId);
        share.MarkAccepted();
        await repository.AddAsync(share, CancellationToken.None);

        var isAccepted = await handler.HandleAsync(new GetCalendarEventShareStatusQuery(recipientId, share.Id), CancellationToken.None);

        Assert.True(isAccepted);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_the_share_was_not_offered_to_the_requesting_user()
    {
        var repository = new InMemoryCalendarEventShareRepository();
        var handler = new GetCalendarEventShareStatusQueryHandler(repository);
        var share = CalendarEventShare.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await repository.AddAsync(share, CancellationToken.None);

        var isAccepted = await handler.HandleAsync(new GetCalendarEventShareStatusQuery(Guid.NewGuid(), share.Id), CancellationToken.None);

        Assert.Null(isAccepted);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_unknown_share_id()
    {
        var handler = new GetCalendarEventShareStatusQueryHandler(new InMemoryCalendarEventShareRepository());

        var isAccepted = await handler.HandleAsync(
            new GetCalendarEventShareStatusQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(isAccepted);
    }
}
