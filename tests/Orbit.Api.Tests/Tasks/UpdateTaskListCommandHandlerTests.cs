using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventories;
using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.UpdateTaskList;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

public sealed class UpdateTaskListCommandHandlerTests
{
    private static UpdateTaskListCommandHandler CreateHandler(InMemoryTaskRepository taskRepository, InMemoryTaskListShareRepository? taskListShareRepository = null)
        => new(
            new TaskListAccessResolver(taskRepository, taskListShareRepository ?? new InMemoryTaskListShareRepository(), new InMemoryUserRepository()),
            taskRepository,
            new TaskListLinkValidator(taskRepository),
            // No inventory tracks these lists, so finishing an entry on one means nothing to a shelf,
            // and no shelf answers an entry on one either.
            new RestockCompletion(
                new InMemoryInventoryManagedTaskListRepository(), new InMemoryInventoryItemRepository(),
                new InMemoryInventoryRepository(), new InMemoryTaskRepository()),
            new StockedEntryCompletion(
                new InMemoryInventoryRepository(), new InMemoryInventoryItemRepository()));

    [Fact]
    public async Task HandleAsync_updates_a_task_list_owned_by_the_requesting_user()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var taskList = TaskList.Create(userId, "Original title", [TaskItem.Create("Original item", null, false)]);
        await repository.AddAsync(taskList, CancellationToken.None);
        var newItems = new[] { TaskItem.Create("New item", null, false) };

        var outcome = await handler.HandleAsync(new UpdateTaskListCommand(userId, taskList.Id, "New title", newItems, IsGroup: false, IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        var stored = await repository.GetByIdAsync(userId, taskList.Id, CancellationToken.None);
        Assert.Equal("New title", stored!.Title);
        Assert.Equal("New item", Assert.Single(stored.Items).Description);
    }

    /// <summary>
    /// A save from a client that knows nothing about categories - the phone, an older tab - is a save
    /// about something else, and must not unfile every entry on the list on its way past. The same rule
    /// the description already follows.
    /// </summary>
    [Fact]
    public async Task An_entry_that_says_nothing_about_its_categories_keeps_them()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var stored = TaskItem.Create("Buy milk", null, false, categories: ["shopping"]);
        var taskList = TaskList.Create(userId, "Errands", [stored]);
        await repository.AddAsync(taskList, CancellationToken.None);

        // The same entry, ticked off, and carrying no categories - which is what an older client sends.
        var incoming = TaskItem.FromPersistence(
            stored.Id, "Buy milk", null, true, null, TaskItemReminders.Default);

        await handler.HandleAsync(
            new UpdateTaskListCommand(
                userId, taskList.Id, "Errands", [incoming], IsGroup: false, IsPrivate: false, EncryptedContent: null,
                EntriesKeepingTheirCategories: new HashSet<Guid> { stored.Id }),
            CancellationToken.None);

        var saved = await repository.GetByIdAsync(userId, taskList.Id, CancellationToken.None);
        Assert.Equal(["shopping"], Assert.Single(saved!.Items).Categories);
    }

    /// <summary>An entry that sent an empty list means "none", and is not in the set above.</summary>
    [Fact]
    public async Task An_entry_sent_with_no_categories_at_all_is_unfiled()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var stored = TaskItem.Create("Buy milk", null, false, categories: ["shopping"]);
        var taskList = TaskList.Create(userId, "Errands", [stored]);
        await repository.AddAsync(taskList, CancellationToken.None);

        var incoming = TaskItem.FromPersistence(
            stored.Id, "Buy milk", null, false, null, TaskItemReminders.Default);

        await handler.HandleAsync(
            new UpdateTaskListCommand(
                userId, taskList.Id, "Errands", [incoming], IsGroup: false, IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        var saved = await repository.GetByIdAsync(userId, taskList.Id, CancellationToken.None);
        Assert.Empty(Assert.Single(saved!.Items).Categories);
    }

    [Fact]
    public async Task HandleAsync_recomputes_completion_after_replacing_the_items()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var taskList = TaskList.Create(userId, "Errands", [TaskItem.Create("Buy milk", null, false)]);
        await repository.AddAsync(taskList, CancellationToken.None);
        var allDoneItems = new[] { TaskItem.Create("Buy milk", null, true) };

        await handler.HandleAsync(new UpdateTaskListCommand(userId, taskList.Id, "Errands", allDoneItems, IsGroup: false, IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, taskList.Id, CancellationToken.None);
        Assert.True(stored!.IsCompleted);
    }

    [Fact]
    public async Task HandleAsync_returns_NotFound_and_does_not_update_a_task_list_owned_by_a_different_user()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var taskList = TaskList.Create(ownerId, "Original title", []);
        await repository.AddAsync(taskList, CancellationToken.None);

        var outcome = await handler.HandleAsync(new UpdateTaskListCommand(otherUserId, taskList.Id, "Hijacked title", [], IsGroup: false, IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
        var stored = await repository.GetByIdAsync(ownerId, taskList.Id, CancellationToken.None);
        Assert.Equal("Original title", stored!.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_NotFound_for_an_unknown_task_list_id()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);

        var outcome = await handler.HandleAsync(new UpdateTaskListCommand(Guid.NewGuid(), Guid.NewGuid(), "Title", [], IsGroup: false, IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
    }

    private static async Task<(InMemoryTaskRepository TaskRepository, Guid OwnerId, Guid RecipientId, TaskList TaskList)> CreateSharedTaskListAsync(
        InMemoryTaskListShareRepository taskListShareRepository, ShareAccessLevel accessLevel)
    {
        var taskRepository = new InMemoryTaskRepository();
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var taskList = TaskList.Create(ownerId, "Original title", []);
        await taskRepository.AddAsync(taskList, CancellationToken.None);
        var share = TaskListShare.Create(taskList.Id, ownerId, recipientId, accessLevel);
        share.MarkAccepted();
        await taskListShareRepository.AddAsync(share, CancellationToken.None);
        return (taskRepository, ownerId, recipientId, taskList);
    }

    [Fact]
    public async Task HandleAsync_returns_ReadOnly_and_does_not_update_a_shared_read_only_task_list()
    {
        var taskListShareRepository = new InMemoryTaskListShareRepository();
        var (taskRepository, _, recipientId, taskList) = await CreateSharedTaskListAsync(taskListShareRepository, ShareAccessLevel.ReadOnly);
        var handler = CreateHandler(taskRepository, taskListShareRepository);

        var outcome = await handler.HandleAsync(new UpdateTaskListCommand(recipientId, taskList.Id, "Edited title", [], IsGroup: false, IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.ReadOnly, outcome.Kind);
    }

    [Fact]
    public async Task HandleAsync_returns_ReadOnly_and_does_not_update_a_task_list_shared_at_the_Share_tier()
    {
        var taskListShareRepository = new InMemoryTaskListShareRepository();
        var (taskRepository, _, recipientId, taskList) = await CreateSharedTaskListAsync(taskListShareRepository, ShareAccessLevel.Share);
        var handler = CreateHandler(taskRepository, taskListShareRepository);

        var outcome = await handler.HandleAsync(new UpdateTaskListCommand(recipientId, taskList.Id, "Edited title", [], IsGroup: false, IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.ReadOnly, outcome.Kind);
    }

    [Fact]
    public async Task HandleAsync_updates_a_shared_task_list_with_edit_access()
    {
        var taskListShareRepository = new InMemoryTaskListShareRepository();
        var (taskRepository, ownerId, recipientId, taskList) = await CreateSharedTaskListAsync(taskListShareRepository, ShareAccessLevel.CanEdit);
        var handler = CreateHandler(taskRepository, taskListShareRepository);

        var outcome = await handler.HandleAsync(new UpdateTaskListCommand(recipientId, taskList.Id, "New title", [], IsGroup: false, IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        var stored = await taskRepository.GetByIdAsync(ownerId, taskList.Id, CancellationToken.None);
        Assert.Equal("New title", stored!.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_Locked_when_someone_else_holds_the_edit_lock()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var taskList = TaskList.Create(userId, "Original title", []);
        taskList.AcquireLock(otherUserId, "otherUser", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        await repository.AddAsync(taskList, CancellationToken.None);

        var outcome = await handler.HandleAsync(new UpdateTaskListCommand(userId, taskList.Id, "Edited title", [], IsGroup: false, IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Locked, outcome.Kind);
        Assert.Equal("otherUser", outcome.LockedByUserName);
    }

    [Fact]
    public async Task HandleAsync_rejects_an_update_that_links_an_item_to_the_list_itself()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var taskList = TaskList.Create(userId, "Errands", []);
        await repository.AddAsync(taskList, CancellationToken.None);
        var itemsLinkingToSelf = new[] { TaskItem.Create("Self reference", null, false, [taskList.Id]) };

        await Assert.ThrowsAsync<InvalidRequestException>(() => handler.HandleAsync(
            new UpdateTaskListCommand(userId, taskList.Id, "Errands", itemsLinkingToSelf, IsGroup: false, IsPrivate: false, EncryptedContent: null), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_saves_the_grouping_flag()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var member = TaskList.Create(userId, "Kitchen", [TaskItem.Create("Paint walls", null, false)]);
        var taskList = TaskList.Create(userId, "Renovation", []);
        await repository.AddAsync(member, CancellationToken.None);
        await repository.AddAsync(taskList, CancellationToken.None);
        var items = new[] { TaskItem.Create("Kitchen done", null, false, [member.Id]) };

        await handler.HandleAsync(
            new UpdateTaskListCommand(userId, taskList.Id, "Renovation", items, IsGroup: true, IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, taskList.Id, CancellationToken.None);
        Assert.True(stored!.IsGroup);
    }

    [Fact]
    public async Task HandleAsync_turns_grouping_back_off_when_the_flag_is_cleared()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var taskList = TaskList.Create(userId, "Renovation", [], isGroup: true);
        await repository.AddAsync(taskList, CancellationToken.None);

        await handler.HandleAsync(
            new UpdateTaskListCommand(userId, taskList.Id, "Renovation", [], IsGroup: false, IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, taskList.Id, CancellationToken.None);
        Assert.False(stored!.IsGroup);
    }
}
