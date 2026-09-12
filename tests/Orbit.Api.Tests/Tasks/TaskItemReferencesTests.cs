using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.DeleteTaskList;
using Orbit.Core.Tasks.UpdateTaskList;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// Entries that are one object on several lists - "Sauce" on the burger list and on the pasta list,
/// picked from the name suggestions. See TaskItem.ReferencesTaskItemId and TaskItemReferences.
///
/// Every member keeps the shared details itself, so what these hold in place is the keeping in step: a
/// save of any member passes them on, the pointers always name the source, and when the source goes the
/// member created first takes its place.
/// </summary>
public sealed class TaskItemReferencesTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private readonly InMemoryTaskRepository _tasks = new();

    private UpdateTaskListCommandHandler Handler()
        => new(
            new TaskListAccessResolver(_tasks, new InMemoryTaskListShareRepository(), new InMemoryUserRepository()),
            _tasks,
            new TaskListLinkValidator(_tasks),
            new RestockCompletion(
                new InMemoryInventoryManagedTaskListRepository(), new InMemoryInventoryItemRepository(),
                new InMemoryInventoryRepository(), new InMemoryTaskRepository()),
            new StockedEntryCompletion(new InMemoryInventoryRepository(), new InMemoryInventoryItemRepository()),
            new InventoryTestContext().ProductEntryPlacement);

    private async Task<TaskList> AListAsync(string title, params TaskItem[] items)
    {
        var taskList = TaskList.Create(UserId, title, items);
        await _tasks.AddAsync(taskList, CancellationToken.None);
        return taskList;
    }

    private Task SaveAsync(TaskList taskList, params TaskItem[] items)
        => Handler().HandleAsync(
            new UpdateTaskListCommand(UserId, taskList.Id, taskList.Title, items, IsGroup: false, IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

    private static TaskItem AnEntry(
        Guid id, string notes = "", Guid? references = null, DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? dueDateUtc = null, bool isCompleted = false)
        => TaskItem.FromPersistence(
            id, "Sauce", dueDateUtc, isCompleted, linkedTaskListIds: null, reminders: null, notes: notes,
            createdAtUtc: createdAtUtc, referencesTaskItemId: references);

    [Fact]
    public async Task A_reference_saved_with_new_details_passes_them_to_its_source_on_another_list()
    {
        var source = AnEntry(Guid.NewGuid(), notes: "Mayonnaise");
        var pasta = await AListAsync("Pasta", source);
        var reference = AnEntry(Guid.NewGuid(), notes: "Mayonnaise", references: source.Id);
        var burger = await AListAsync("Burger", reference);

        await SaveAsync(burger, AnEntry(reference.Id, notes: "Garlic mayonnaise", references: source.Id));

        Assert.Equal("Garlic mayonnaise", Assert.Single(pasta.Items).Notes);
    }

    /// <summary>
    /// A newcomer takes on what its group says rather than overwriting it: a phone that picked the name
    /// knows only the words, and a save of those alone must not empty what every other member carries.
    /// </summary>
    [Fact]
    public async Task A_new_member_takes_on_what_its_group_says_rather_than_overwriting_it()
    {
        var source = AnEntry(Guid.NewGuid(), notes: "Mayonnaise");
        var pasta = await AListAsync("Pasta", source);
        var burger = await AListAsync("Burger");

        await SaveAsync(burger, AnEntry(Guid.NewGuid(), notes: string.Empty, references: source.Id));

        Assert.Equal("Mayonnaise", Assert.Single(pasta.Items).Notes);
        Assert.Equal("Mayonnaise", Assert.Single(burger.Items).Notes);
    }

    /// <summary>What is shared is the thing, not the plan: its own date and tick stay each entry's own.</summary>
    [Fact]
    public async Task Its_own_date_and_tick_are_not_passed_on()
    {
        var due = DateTimeOffset.UtcNow.AddDays(3);
        var source = AnEntry(Guid.NewGuid(), dueDateUtc: due, isCompleted: true);
        var pasta = await AListAsync("Pasta", source);
        var reference = AnEntry(Guid.NewGuid(), references: source.Id);
        var burger = await AListAsync("Burger", reference);

        await SaveAsync(burger, AnEntry(reference.Id, notes: "Garlic", references: source.Id));

        var stillThere = Assert.Single(pasta.Items);
        Assert.Equal(due, stillThere.DueDateUtc);
        Assert.True(stillThere.IsCompleted);
    }

    [Fact]
    public async Task A_reference_to_a_reference_is_pointed_at_the_source()
    {
        var source = AnEntry(Guid.NewGuid());
        await AListAsync("Pasta", source);
        var middle = AnEntry(Guid.NewGuid(), references: source.Id);
        await AListAsync("Burger", middle);
        var salad = await AListAsync("Salad");

        var newcomer = Guid.NewGuid();
        await SaveAsync(salad, AnEntry(newcomer, references: middle.Id));

        Assert.Equal(source.Id, Assert.Single(salad.Items).ReferencesTaskItemId);
    }

    [Fact]
    public async Task A_reference_to_nothing_is_an_entry_of_its_own()
    {
        var salad = await AListAsync("Salad");

        await SaveAsync(salad, AnEntry(Guid.NewGuid(), references: Guid.NewGuid()));

        Assert.Null(Assert.Single(salad.Items).ReferencesTaskItemId);
    }

    /// <summary>
    /// The user's rule: when the source is deleted, the member created first takes its role, and the rest
    /// of the group points at that one - nothing is lost, since every member already carries the details.
    /// </summary>
    [Fact]
    public async Task Removing_the_source_hands_its_role_to_the_member_created_first()
    {
        var source = AnEntry(Guid.NewGuid());
        var pasta = await AListAsync("Pasta", source);
        var first = AnEntry(Guid.NewGuid(), references: source.Id, createdAtUtc: DateTimeOffset.UtcNow.AddDays(-2));
        var burger = await AListAsync("Burger", first);
        var second = AnEntry(Guid.NewGuid(), references: source.Id, createdAtUtc: DateTimeOffset.UtcNow.AddDays(-1));
        var salad = await AListAsync("Salad", second);

        await SaveAsync(pasta);

        Assert.Null(Assert.Single(burger.Items).ReferencesTaskItemId);
        Assert.Equal(first.Id, Assert.Single(salad.Items).ReferencesTaskItemId);
    }

    [Fact]
    public async Task Deleting_the_list_that_holds_the_source_hands_its_role_on_too()
    {
        var source = AnEntry(Guid.NewGuid());
        var pasta = await AListAsync("Pasta", source);
        var first = AnEntry(Guid.NewGuid(), references: source.Id, createdAtUtc: DateTimeOffset.UtcNow.AddDays(-2));
        var burger = await AListAsync("Burger", first);
        var second = AnEntry(Guid.NewGuid(), references: source.Id, createdAtUtc: DateTimeOffset.UtcNow.AddDays(-1));
        var salad = await AListAsync("Salad", second);

        await new DeleteTaskListCommandHandler(_tasks, new InMemoryTaskListShareRepository(), new InMemorySyncTombstoneRepository())
            .HandleAsync(new DeleteTaskListCommand(UserId, pasta.Id), CancellationToken.None);

        Assert.Null(Assert.Single(burger.Items).ReferencesTaskItemId);
        Assert.Equal(first.Id, Assert.Single(salad.Items).ReferencesTaskItemId);
    }

    [Fact]
    public async Task An_entry_keeps_the_creation_time_it_was_first_stored_with()
    {
        var firstStored = DateTimeOffset.UtcNow.AddDays(-5);
        var entry = AnEntry(Guid.NewGuid(), createdAtUtc: firstStored);
        var burger = await AListAsync("Burger", entry);

        await SaveAsync(burger, AnEntry(entry.Id, notes: "Changed"), AnEntry(Guid.NewGuid()));

        Assert.Equal(firstStored, burger.Items[0].CreatedAtUtc);
        Assert.NotNull(burger.Items[1].CreatedAtUtc);
    }

    /// <summary>A phone build that knows nothing of references saves the entry without one, and it keeps its own.</summary>
    [Fact]
    public async Task A_save_that_says_nothing_about_the_reference_keeps_it()
    {
        var source = AnEntry(Guid.NewGuid());
        await AListAsync("Pasta", source);
        var reference = AnEntry(Guid.NewGuid(), references: source.Id);
        var burger = await AListAsync("Burger", reference);

        await Handler().HandleAsync(
            new UpdateTaskListCommand(
                UserId, burger.Id, "Burger", [AnEntry(reference.Id)], IsGroup: false, IsPrivate: false,
                EncryptedContent: null, EntriesKeepingTheirReference: new HashSet<Guid> { reference.Id }),
            CancellationToken.None);

        Assert.Equal(source.Id, Assert.Single(burger.Items).ReferencesTaskItemId);
    }

    /// <summary>The entry's own minimum is written on its product while there is one, and is the one detail not shared.</summary>
    [Fact]
    public void An_entrys_minimum_is_its_own_and_its_product_says_the_same()
    {
        var entry = TaskItem.Create(
            "Flour", null, false,
            subject: new TaskItemSubject(TaskItemKind.Inventory),
            product: TaskItemProduct.Default with { MinimumQuantity = 2 },
            requiredQuantity: 5);

        Assert.Equal(5, entry.RequiredQuantity);
        Assert.Equal(5, entry.Product!.MinimumQuantity);
    }
}
