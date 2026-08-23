using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notes;
using Orbit.Core.Notes.GetNoteShareStatus;
using Xunit;

namespace Orbit.Api.Tests.Notes;

public sealed class GetNoteShareStatusQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_false_for_a_pending_share()
    {
        var repository = new InMemoryNoteShareRepository();
        var handler = new GetNoteShareStatusQueryHandler(repository);
        var recipientId = Guid.NewGuid();
        var share = NoteShare.Create(Guid.NewGuid(), Guid.NewGuid(), recipientId);
        await repository.AddAsync(share, CancellationToken.None);

        var isAccepted = await handler.HandleAsync(new GetNoteShareStatusQuery(recipientId, share.Id), CancellationToken.None);

        Assert.False(isAccepted);
    }

    [Fact]
    public async Task HandleAsync_returns_true_once_the_share_has_been_accepted()
    {
        var repository = new InMemoryNoteShareRepository();
        var handler = new GetNoteShareStatusQueryHandler(repository);
        var recipientId = Guid.NewGuid();
        var share = NoteShare.Create(Guid.NewGuid(), Guid.NewGuid(), recipientId);
        share.MarkAccepted();
        await repository.AddAsync(share, CancellationToken.None);

        var isAccepted = await handler.HandleAsync(new GetNoteShareStatusQuery(recipientId, share.Id), CancellationToken.None);

        Assert.True(isAccepted);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_the_share_was_not_offered_to_the_requesting_user()
    {
        var repository = new InMemoryNoteShareRepository();
        var handler = new GetNoteShareStatusQueryHandler(repository);
        var share = NoteShare.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await repository.AddAsync(share, CancellationToken.None);

        var isAccepted = await handler.HandleAsync(new GetNoteShareStatusQuery(Guid.NewGuid(), share.Id), CancellationToken.None);

        Assert.Null(isAccepted);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_unknown_share_id()
    {
        var handler = new GetNoteShareStatusQueryHandler(new InMemoryNoteShareRepository());

        var isAccepted = await handler.HandleAsync(
            new GetNoteShareStatusQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(isAccepted);
    }
}
