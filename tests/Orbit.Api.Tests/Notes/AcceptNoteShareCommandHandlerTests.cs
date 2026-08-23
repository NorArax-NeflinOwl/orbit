using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notes;
using Orbit.Core.Notes.AcceptNoteShare;
using Xunit;

namespace Orbit.Api.Tests.Notes;

public sealed class AcceptNoteShareCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_marks_the_share_accepted()
    {
        var shareRepository = new InMemoryNoteShareRepository();
        var handler = new AcceptNoteShareCommandHandler(shareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var share = NoteShare.Create(Guid.NewGuid(), ownerId, recipientId);
        await shareRepository.AddAsync(share, CancellationToken.None);

        var accepted = await handler.HandleAsync(new AcceptNoteShareCommand(recipientId, share.Id), CancellationToken.None);

        Assert.True(accepted);
        var stored = await shareRepository.GetByIdAsync(recipientId, share.Id, CancellationToken.None);
        Assert.True(stored!.IsAccepted);
    }

    [Fact]
    public async Task HandleAsync_returns_false_when_the_share_was_not_offered_to_the_requesting_user()
    {
        var shareRepository = new InMemoryNoteShareRepository();
        var handler = new AcceptNoteShareCommandHandler(shareRepository);
        var share = NoteShare.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await shareRepository.AddAsync(share, CancellationToken.None);

        var accepted = await handler.HandleAsync(new AcceptNoteShareCommand(Guid.NewGuid(), share.Id), CancellationToken.None);

        Assert.False(accepted);
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_an_unknown_share_id()
    {
        var handler = new AcceptNoteShareCommandHandler(new InMemoryNoteShareRepository());

        var accepted = await handler.HandleAsync(new AcceptNoteShareCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(accepted);
    }

    [Fact]
    public async Task HandleAsync_is_idempotent_when_accepted_twice()
    {
        var shareRepository = new InMemoryNoteShareRepository();
        var handler = new AcceptNoteShareCommandHandler(shareRepository);
        var recipientId = Guid.NewGuid();
        var share = NoteShare.Create(Guid.NewGuid(), Guid.NewGuid(), recipientId);
        await shareRepository.AddAsync(share, CancellationToken.None);

        var command = new AcceptNoteShareCommand(recipientId, share.Id);
        await handler.HandleAsync(command, CancellationToken.None);
        var acceptedAgain = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(acceptedAgain);
    }
}
