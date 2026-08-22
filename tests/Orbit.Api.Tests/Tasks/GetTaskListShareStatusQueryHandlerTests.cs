using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.GetTaskListShareStatus;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

public sealed class GetTaskListShareStatusQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_false_for_a_pending_share()
    {
        var repository = new InMemoryTaskListShareRepository();
        var handler = new GetTaskListShareStatusQueryHandler(repository);
        var recipientId = Guid.NewGuid();
        var share = TaskListShare.Create(Guid.NewGuid(), Guid.NewGuid(), recipientId);
        await repository.AddAsync(share, CancellationToken.None);

        var isAccepted = await handler.HandleAsync(new GetTaskListShareStatusQuery(recipientId, share.Id), CancellationToken.None);

        Assert.False(isAccepted);
    }

    [Fact]
    public async Task HandleAsync_returns_true_once_the_share_has_been_accepted()
    {
        var repository = new InMemoryTaskListShareRepository();
        var handler = new GetTaskListShareStatusQueryHandler(repository);
        var recipientId = Guid.NewGuid();
        var share = TaskListShare.Create(Guid.NewGuid(), Guid.NewGuid(), recipientId);
        share.MarkAccepted(Guid.NewGuid());
        await repository.AddAsync(share, CancellationToken.None);

        var isAccepted = await handler.HandleAsync(new GetTaskListShareStatusQuery(recipientId, share.Id), CancellationToken.None);

        Assert.True(isAccepted);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_the_share_was_not_offered_to_the_requesting_user()
    {
        var repository = new InMemoryTaskListShareRepository();
        var handler = new GetTaskListShareStatusQueryHandler(repository);
        var share = TaskListShare.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await repository.AddAsync(share, CancellationToken.None);

        var isAccepted = await handler.HandleAsync(new GetTaskListShareStatusQuery(Guid.NewGuid(), share.Id), CancellationToken.None);

        Assert.Null(isAccepted);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_unknown_share_id()
    {
        var handler = new GetTaskListShareStatusQueryHandler(new InMemoryTaskListShareRepository());

        var isAccepted = await handler.HandleAsync(
            new GetTaskListShareStatusQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(isAccepted);
    }
}
