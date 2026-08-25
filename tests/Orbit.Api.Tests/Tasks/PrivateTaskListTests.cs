using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.CreateTaskList;
using Orbit.Core.Tasks.MoveTaskItem;
using Orbit.Core.Tasks.ShareTaskList;
using Orbit.Core.Tasks.UpdateTaskList;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// Mirrors PrivateNoteTests for task lists, plus the two things only lists have: items that move between
/// lists, and a completion flag the server derives - neither of which can work on content it can't read.
/// </summary>
public sealed class PrivateTaskListTests
{
    private static readonly EncryptedPayload SealedContent = new("c2VhbGVk", "bm9uY2U=");

    [Fact]
    public async Task A_private_list_keeps_nothing_readable_on_the_server()
    {
        var context = new PrivateTaskListTestContext();

        var taskListId = await context.CreateAsync("Therapy", [TaskItem.Create("Book a session", null, false)], isPrivate: true, SealedContent);

        var stored = await context.TaskRepository.GetByIdAsync(context.OwnerId, taskListId, CancellationToken.None);
        Assert.Equal(string.Empty, stored!.Title);
        Assert.Empty(stored.Items);
        Assert.Equal(SealedContent, stored.EncryptedContent);
        Assert.True(stored.IsPrivate);
    }

    [Fact]
    public async Task A_private_list_reports_itself_incomplete_because_the_server_sees_no_items()
    {
        var context = new PrivateTaskListTestContext();

        var taskListId = await context.CreateAsync("Therapy", [TaskItem.Create("Done already", null, true)], isPrivate: true, SealedContent);

        // Completion is derived from items, and a private list has none here - the browser recomputes it
        // after opening the sealed content (see TasksApiClient.OpenIfPrivateAsync).
        var stored = await context.TaskRepository.GetByIdAsync(context.OwnerId, taskListId, CancellationToken.None);
        Assert.False(stored!.IsCompleted);
    }

    [Fact]
    public async Task Claiming_privacy_without_sealed_content_is_refused()
    {
        var context = new PrivateTaskListTestContext();

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => context.CreateAsync("Therapy", [TaskItem.Create("Secret", null, false)], isPrivate: true, encryptedContent: null));
    }

    [Fact]
    public async Task Turning_privacy_on_clears_the_items_that_were_readable_before()
    {
        var context = new PrivateTaskListTestContext();
        var taskListId = await context.CreateAsync("Errands", [TaskItem.Create("Buy milk", null, false)], isPrivate: false, encryptedContent: null);

        await context.UpdateAsync(taskListId, "Errands", [TaskItem.Create("Buy milk", null, false)], isPrivate: true, SealedContent);

        var stored = await context.TaskRepository.GetByIdAsync(context.OwnerId, taskListId, CancellationToken.None);
        Assert.Equal(string.Empty, stored!.Title);
        Assert.Empty(stored.Items);
    }

    [Fact]
    public async Task A_private_list_cannot_be_shared()
    {
        var context = new PrivateTaskListTestContext();
        var taskListId = await context.CreateAsync("Therapy", [], isPrivate: true, SealedContent);

        await Assert.ThrowsAsync<InvalidRequestException>(() => context.ShareAsync(taskListId, Guid.NewGuid()));
    }

    [Fact]
    public async Task An_ordinary_shared_list_still_resolves_for_its_recipient()
    {
        // The control for the test below - see PrivateNoteTests for why it is its own test.
        var context = new PrivateTaskListTestContext();
        var recipientId = Guid.NewGuid();
        var taskListId = await context.CreateAsync("Errands", [TaskItem.Create("Buy milk", null, false)], isPrivate: false, encryptedContent: null);
        await context.ShareAndAcceptAsync(taskListId, recipientId);

        Assert.NotNull(await context.ResolveForAsync(recipientId, taskListId));
    }

    [Fact]
    public async Task An_existing_share_stops_granting_access_once_the_list_becomes_private()
    {
        var context = new PrivateTaskListTestContext();
        var recipientId = Guid.NewGuid();
        var taskListId = await context.CreateAsync("Errands", [TaskItem.Create("Buy milk", null, false)], isPrivate: false, encryptedContent: null);
        await context.ShareAndAcceptAsync(taskListId, recipientId);

        await context.UpdateAsync(taskListId, "Errands", [], isPrivate: true, SealedContent);

        Assert.Null(await context.ResolveForAsync(recipientId, taskListId));
        Assert.NotNull(await context.ResolveForAsync(context.OwnerId, taskListId));
    }

    [Fact]
    public async Task Moving_an_item_into_a_private_list_is_refused_rather_than_losing_it()
    {
        var context = new PrivateTaskListTestContext();
        var item = TaskItem.Create("Buy milk", null, false);
        var sourceId = await context.CreateAsync("Errands", [item], isPrivate: false, encryptedContent: null);
        var privateId = await context.CreateAsync("Therapy", [], isPrivate: true, SealedContent);

        // Allowed through, the item would be taken off the source and then dropped when the target
        // sealed itself again - the move has to be refused, not merely fail to arrive.
        await Assert.ThrowsAsync<InvalidRequestException>(() => context.MoveItemAsync(sourceId, item.Id, privateId));

        var source = await context.TaskRepository.GetByIdAsync(context.OwnerId, sourceId, CancellationToken.None);
        Assert.Equal("Buy milk", Assert.Single(source!.Items).Description);
    }

    [Fact]
    public async Task Moving_an_item_out_of_a_private_list_is_refused_too()
    {
        var context = new PrivateTaskListTestContext();
        var privateId = await context.CreateAsync("Therapy", [], isPrivate: true, SealedContent);
        var targetId = await context.CreateAsync("Errands", [], isPrivate: false, encryptedContent: null);

        await Assert.ThrowsAsync<InvalidRequestException>(() => context.MoveItemAsync(privateId, Guid.NewGuid(), targetId));
    }

    /// <summary>The collaborator graph these flows need, wired the way DI wires the real one.</summary>
    private sealed class PrivateTaskListTestContext
    {
        public InMemoryTaskRepository TaskRepository { get; } = new();
        public InMemoryTaskListShareRepository TaskListShareRepository { get; } = new();
        public InMemoryUserRepository UserRepository { get; } = new();
        public Guid OwnerId { get; } = Guid.NewGuid();

        private TaskListAccessResolver Resolver => new(TaskRepository, TaskListShareRepository, UserRepository);

        public Task<Guid> CreateAsync(string title, IReadOnlyList<TaskItem> items, bool isPrivate, EncryptedPayload? encryptedContent)
            => new CreateTaskListCommandHandler(TaskRepository, new TaskListLinkValidator(TaskRepository))
                .HandleAsync(new CreateTaskListCommand(OwnerId, title, items, IsGroup: false, isPrivate, encryptedContent), CancellationToken.None);

        public Task<EditOutcome> UpdateAsync(
            Guid taskListId, string title, IReadOnlyList<TaskItem> items, bool isPrivate, EncryptedPayload? encryptedContent)
            => new UpdateTaskListCommandHandler(Resolver, TaskRepository, new TaskListLinkValidator(TaskRepository))
                .HandleAsync(
                    new UpdateTaskListCommand(OwnerId, taskListId, title, items, IsGroup: false, isPrivate, encryptedContent),
                    CancellationToken.None);

        public Task<ShareOutcome?> ShareAsync(Guid taskListId, Guid recipientId)
            => new ShareTaskListCommandHandler(Resolver, TaskListShareRepository)
                .HandleAsync(new ShareTaskListCommand(OwnerId, taskListId, recipientId, ShareAccessLevel.ReadOnly), CancellationToken.None);

        public Task<EditOutcome> MoveItemAsync(Guid sourceTaskListId, Guid itemId, Guid targetTaskListId)
            => new MoveTaskItemCommandHandler(Resolver, TaskRepository, new TaskListLinkValidator(TaskRepository))
                .HandleAsync(new MoveTaskItemCommand(OwnerId, sourceTaskListId, itemId, targetTaskListId), CancellationToken.None);

        public async Task ShareAndAcceptAsync(Guid taskListId, Guid recipientId)
        {
            var outcome = await ShareAsync(taskListId, recipientId);
            var share = await TaskListShareRepository.GetByIdAsync(recipientId, outcome!.ShareId, CancellationToken.None);
            share!.MarkAccepted();
            await TaskListShareRepository.UpdateAsync(share, CancellationToken.None);
        }

        public Task<TaskList?> ResolveForAsync(Guid callerId, Guid taskListId)
            => Resolver.ResolveAsync(callerId, taskListId, CancellationToken.None);
    }
}
