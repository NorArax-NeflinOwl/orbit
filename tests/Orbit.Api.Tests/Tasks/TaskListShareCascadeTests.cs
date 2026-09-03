using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Sharing;
using Orbit.Core.Sharing.ClaimPublicShareLink;
using Orbit.Core.Sharing.CreatePublicShareLink;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.AcceptTaskListShare;
using Orbit.Core.Tasks.ShareTaskList;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// Sharing a group list hands over the whole of it: the lists it gathers, the lists those gather, and
/// the inventory the work is measured against. The shape here is the one that showed the gap - a group
/// list arrived on its own, so every row on it pointed at something the recipient could not open.
/// </summary>
public sealed class TaskListShareCascadeTests
{
    [Fact]
    public async Task Sharing_a_group_list_shares_the_lists_it_links_to()
    {
        var context = new CascadeTestContext();
        var member = await context.AddTaskListAsync("Shopping");
        var group = await context.AddGroupListAsync("Saturday", member.Id);

        await context.ShareAsync(group.Id, ShareAccessLevel.ReadOnly);

        var memberShare = await context.TaskListShareRepository.FindExistingAsync(
            member.Id, context.RecipientId, CancellationToken.None);
        Assert.NotNull(memberShare);
        Assert.Equal(ShareAccessLevel.ReadOnly, memberShare!.AccessLevel);
    }

    [Fact]
    public async Task Sharing_a_group_list_follows_the_links_all_the_way_down()
    {
        var context = new CascadeTestContext();
        var leaf = await context.AddTaskListAsync("Tools");
        var middle = await context.AddGroupListAsync("Workshop", leaf.Id);
        var group = await context.AddGroupListAsync("Saturday", middle.Id);

        await context.ShareAsync(group.Id, ShareAccessLevel.ReadOnly);

        Assert.NotNull(await context.TaskListShareRepository.FindExistingAsync(leaf.Id, context.RecipientId, CancellationToken.None));
    }

    [Fact]
    public async Task Sharing_a_group_list_shares_the_inventory_it_is_measured_against()
    {
        var context = new CascadeTestContext();
        var inventory = await context.AddInventoryAsync("Pantry");
        var member = await context.AddTaskListAsync("Shopping");
        var group = await context.AddGroupListAsync("Saturday", member.Id);
        await context.MeasureAgainstAsync(group, inventory.Id);

        await context.ShareAsync(group.Id, ShareAccessLevel.ReadOnly);

        Assert.NotNull(await context.InventoryShareRepository.FindExistingAsync(
            inventory.Id, context.RecipientId, CancellationToken.None));
    }

    [Fact]
    public async Task A_private_linked_list_is_left_behind()
    {
        var context = new CascadeTestContext();
        var privateMember = await context.AddPrivateTaskListAsync();
        var group = await context.AddGroupListAsync("Saturday", privateMember.Id);

        await context.ShareAsync(group.Id, ShareAccessLevel.ReadOnly);

        // Its items are sealed in the owner's browser, so a grant would only hand over ciphertext.
        Assert.Null(await context.TaskListShareRepository.FindExistingAsync(
            privateMember.Id, context.RecipientId, CancellationToken.None));
    }

    [Fact]
    public async Task The_whole_tree_is_still_waiting_until_the_offer_is_accepted()
    {
        var context = new CascadeTestContext();
        var member = await context.AddTaskListAsync("Shopping");
        var group = await context.AddGroupListAsync("Saturday", member.Id);

        await context.ShareAsync(group.Id, ShareAccessLevel.ReadOnly);

        var memberShare = await context.TaskListShareRepository.FindExistingAsync(
            member.Id, context.RecipientId, CancellationToken.None);
        Assert.False(memberShare!.IsAccepted);
    }

    [Fact]
    public async Task Accepting_the_offer_opens_the_whole_tree_at_once()
    {
        var context = new CascadeTestContext();
        var inventory = await context.AddInventoryAsync("Pantry");
        var member = await context.AddTaskListAsync("Shopping");
        var group = await context.AddGroupListAsync("Saturday", member.Id);
        await context.MeasureAgainstAsync(group, inventory.Id);
        var outcome = await context.ShareAsync(group.Id, ShareAccessLevel.ReadOnly);

        await context.AcceptAsync(outcome!.ShareId);

        Assert.NotNull(await context.TaskListShareRepository.FindAcceptedGrantAsync(
            member.Id, context.RecipientId, CancellationToken.None));
        Assert.NotNull(await context.InventoryShareRepository.FindAcceptedGrantAsync(
            inventory.Id, context.RecipientId, CancellationToken.None));
    }

    [Fact]
    public async Task A_list_added_to_the_group_after_the_offer_was_accepted_is_shared_too()
    {
        var context = new CascadeTestContext();
        var member = await context.AddTaskListAsync("Shopping");
        var group = await context.AddGroupListAsync("Saturday", member.Id);
        var outcome = await context.ShareAsync(group.Id, ShareAccessLevel.ReadOnly);
        await context.AcceptAsync(outcome!.ShareId);

        var latecomer = await context.AddTaskListAsync("Tools");
        await context.AddLinkAsync(group, latecomer.Id);
        await context.ShareAsync(group.Id, ShareAccessLevel.ReadOnly);

        // Already accepted at the top, so the new arrival is not left as a second thing to agree to.
        Assert.NotNull(await context.TaskListShareRepository.FindAcceptedGrantAsync(
            latecomer.Id, context.RecipientId, CancellationToken.None));
    }

    [Fact]
    public async Task Claiming_a_link_to_a_group_list_claims_what_it_gathers()
    {
        var context = new CascadeTestContext();
        var inventory = await context.AddInventoryAsync("Pantry");
        var member = await context.AddTaskListAsync("Shopping");
        var group = await context.AddGroupListAsync("Saturday", member.Id);
        await context.MeasureAgainstAsync(group, inventory.Id);

        await context.ClaimLinkToAsync(group.Id);

        // Nothing left to accept, exactly like the link's own grant - the claimer asked for it.
        Assert.NotNull(await context.TaskListShareRepository.FindAcceptedGrantAsync(
            member.Id, context.RecipientId, CancellationToken.None));
        Assert.NotNull(await context.InventoryShareRepository.FindAcceptedGrantAsync(
            inventory.Id, context.RecipientId, CancellationToken.None));
    }

    private sealed class CascadeTestContext
    {
        private readonly InMemoryTaskRepository _taskRepository = new();
        private readonly InMemoryInventoryRepository _inventoryRepository = new();
        private readonly InMemoryUserRepository _userRepository = new();
        private readonly InMemoryPublicShareLinkRepository _publicShareLinkRepository = new();
        private readonly PublicSharedItemReader _publicSharedItemReader;

        public InMemoryTaskListShareRepository TaskListShareRepository { get; } = new();
        public InMemoryInventoryShareRepository InventoryShareRepository { get; } = new();
        public Guid OwnerId { get; }
        public Guid RecipientId { get; } = Guid.NewGuid();

        public CascadeTestContext()
        {
            var owner = User.Create("anna@example.com", "anna", "Anna Kowalska", "hash");
            OwnerId = owner.Id;
            _userRepository.AddAsync(owner, CancellationToken.None).GetAwaiter().GetResult();
            _publicSharedItemReader = new PublicSharedItemReader(
                new InMemoryNoteRepository(), _taskRepository, new InMemoryCalendarEventRepository(),
                _inventoryRepository, new InMemoryInventoryItemRepository(), _userRepository);
        }

        private TaskListShareCascade Cascade => new(
            _taskRepository, _inventoryRepository, TaskListShareRepository, InventoryShareRepository);

        public async Task<TaskList> AddTaskListAsync(string title)
        {
            var taskList = TaskList.Create(OwnerId, title, [TaskItem.Create("Something", null, false)]);
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList;
        }

        public async Task<TaskList> AddPrivateTaskListAsync()
        {
            var taskList = TaskList.Create(
                OwnerId, string.Empty, [], isPrivate: true, encryptedContent: new EncryptedPayload("c2VhbGVk", "bm9uY2U="));
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList;
        }

        public async Task<TaskList> AddGroupListAsync(string title, Guid memberId)
        {
            var taskList = TaskList.Create(
                OwnerId, title, [TaskItem.Create(title, null, false, [memberId])], isGroup: true);
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList;
        }

        public async Task AddLinkAsync(TaskList group, Guid memberId)
        {
            group.Update(
                group.Title, [.. group.Items, TaskItem.Create("Also", null, false, [memberId])],
                group.IsGroup, group.IsPrivate, group.EncryptedContent, group.Priority);
            await _taskRepository.UpdateAsync(group, CancellationToken.None);
        }

        public async Task<Inventory> AddInventoryAsync(string name)
        {
            var inventory = Inventory.Create(OwnerId, name);
            await _inventoryRepository.AddAsync(inventory, CancellationToken.None);
            return inventory;
        }

        public async Task MeasureAgainstAsync(TaskList taskList, Guid inventoryId)
        {
            taskList.LinkToInventory(inventoryId);
            await _taskRepository.UpdateAsync(taskList, CancellationToken.None);
        }

        public Task<ShareOutcome?> ShareAsync(Guid taskListId, ShareAccessLevel accessLevel)
            => new ShareTaskListCommandHandler(
                    new TaskListAccessResolver(_taskRepository, TaskListShareRepository, _userRepository),
                    TaskListShareRepository, Cascade, new RecordingSharedItemNotifier())
                .HandleAsync(new ShareTaskListCommand(OwnerId, taskListId, RecipientId, accessLevel), CancellationToken.None);

        public Task<bool> AcceptAsync(Guid shareId)
            => new AcceptTaskListShareCommandHandler(TaskListShareRepository, Cascade)
                .HandleAsync(new AcceptTaskListShareCommand(RecipientId, shareId), CancellationToken.None);

        public async Task ClaimLinkToAsync(Guid taskListId)
        {
            var link = await new CreatePublicShareLinkCommandHandler(_publicShareLinkRepository, _publicSharedItemReader)
                .HandleAsync(new CreatePublicShareLinkCommand(OwnerId, SharedItemType.TaskList, taskListId), CancellationToken.None);
            await new ClaimPublicShareLinkCommandHandler(
                    _publicShareLinkRepository, _publicSharedItemReader, new InMemoryNoteShareRepository(),
                    TaskListShareRepository, new InMemoryCalendarEventShareRepository(), InventoryShareRepository,
                    Cascade, new RecordingSharedItemNotifier())
                .HandleAsync(new ClaimPublicShareLinkCommand(link!.Token, RecipientId), CancellationToken.None);
        }
    }
}
