using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventory;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.CreateTaskList;
using Orbit.Core.Tasks.UpdateTaskList;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// What a task list is for, and the one thing that kind brings with it. A location only means anything
/// on a calendar list, so these are mostly about it not surviving anywhere else.
/// </summary>
public sealed class TaskListKindTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly InMemoryTaskRepository _taskRepository = new();

    private CreateTaskListCommandHandler CreateHandler
        => new(_taskRepository, new TaskListLinkValidator(_taskRepository));

    private UpdateTaskListCommandHandler UpdateHandler
        => new(
            new TaskListAccessResolver(_taskRepository, new InMemoryTaskListShareRepository(), new InMemoryUserRepository()),
            _taskRepository,
            new TaskListLinkValidator(_taskRepository),
            new RestockCompletion(new InMemoryInventoryManagedTaskListRepository(), new InMemoryInventoryRepository()));

    private Task<TaskList?> ReadAsync(Guid taskListId)
        => _taskRepository.GetByIdAsync(_userId, taskListId, CancellationToken.None);

    [Fact]
    public async Task A_list_is_an_ordinary_checklist_unless_it_says_otherwise()
    {
        var id = await CreateHandler.HandleAsync(
            new CreateTaskListCommand(_userId, "Zakupy", [], IsGroup: false, IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        var taskList = await ReadAsync(id);
        Assert.Equal(TaskListKind.Checklist, taskList!.Kind);
        Assert.Equal(string.Empty, taskList.Location);
    }

    [Fact]
    public async Task A_calendar_list_keeps_where_it_happens()
    {
        var id = await CreateHandler.HandleAsync(
            new CreateTaskListCommand(
                _userId, "Wizyty", [], IsGroup: false, IsPrivate: false, EncryptedContent: null,
                Kind: TaskListKind.Calendar, Location: "  Przychodnia, ul. Długa 4  "),
            CancellationToken.None);

        var taskList = await ReadAsync(id);
        Assert.Equal(TaskListKind.Calendar, taskList!.Kind);
        // Trimmed, since it is written by hand and read back beside a title that is trimmed too.
        Assert.Equal("Przychodnia, ul. Długa 4", taskList.Location);
    }

    [Fact]
    public async Task A_checklist_has_nowhere_to_be_even_if_it_is_told_one()
    {
        var id = await CreateHandler.HandleAsync(
            new CreateTaskListCommand(
                _userId, "Zakupy", [], IsGroup: false, IsPrivate: false, EncryptedContent: null,
                Location: "Przychodnia"),
            CancellationToken.None);

        Assert.Equal(string.Empty, (await ReadAsync(id))!.Location);
    }

    [Fact]
    public async Task Turning_a_calendar_list_back_into_a_checklist_drops_where_it_used_to_be()
    {
        var id = await CreateHandler.HandleAsync(
            new CreateTaskListCommand(
                _userId, "Wizyty", [], IsGroup: false, IsPrivate: false, EncryptedContent: null,
                Kind: TaskListKind.Calendar, Location: "Przychodnia"),
            CancellationToken.None);

        await UpdateHandler.HandleAsync(
            new UpdateTaskListCommand(
                _userId, id, "Wizyty", [], IsGroup: false, IsPrivate: false, EncryptedContent: null,
                Kind: TaskListKind.Checklist, Location: "Przychodnia"),
            CancellationToken.None);

        // Otherwise it would resurface the moment somebody changed their mind back again.
        var taskList = await ReadAsync(id);
        Assert.Equal(TaskListKind.Checklist, taskList!.Kind);
        Assert.Equal(string.Empty, taskList.Location);
    }

    [Fact]
    public async Task An_existing_list_can_be_made_a_calendar_list()
    {
        var id = await CreateHandler.HandleAsync(
            new CreateTaskListCommand(_userId, "Wizyty", [], IsGroup: false, IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        await UpdateHandler.HandleAsync(
            new UpdateTaskListCommand(
                _userId, id, "Wizyty", [], IsGroup: false, IsPrivate: false, EncryptedContent: null,
                Kind: TaskListKind.Calendar, Location: "Przychodnia"),
            CancellationToken.None);

        var taskList = await ReadAsync(id);
        Assert.Equal(TaskListKind.Calendar, taskList!.Kind);
        Assert.Equal("Przychodnia", taskList.Location);
    }
}
