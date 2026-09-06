using Orbit.Api.Tests.TestDoubles;
using Orbit.Core;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.UpdateTaskList;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// The longer text on an entry - what it is about, as opposed to what it is called. A calendar entry had
/// one all along, on its event; every other kind could only ever be as long as its own line. See
/// TaskItem.Notes, and its name, which is what Description was already taken by.
/// </summary>
public sealed class TaskItemNotesTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void An_entry_nobody_wrote_one_on_carries_an_empty_one()
        => Assert.Equal(string.Empty, TaskItem.Create("Buy milk", dueDateUtc: null, isCompleted: false).Notes);

    [Fact]
    public void An_entry_keeps_what_was_written_on_it()
    {
        var entry = TaskItem.Create(
            "Buy milk", dueDateUtc: null, isCompleted: false, notes: "The blue one, not the green.");

        Assert.Equal("The blue one, not the green.", entry.Notes);
    }

    /// <summary>Every kind, which is the point of it: this used to belong to appointments alone.</summary>
    [Theory]
    [InlineData(TaskItemKind.Checklist)]
    [InlineData(TaskItemKind.Calendar)]
    [InlineData(TaskItemKind.Inventory)]
    public void Any_kind_of_entry_can_carry_one(TaskItemKind kind)
    {
        var entry = TaskItem.Create(
            "Buy milk", dueDateUtc: null, isCompleted: false, subject: new TaskItemSubject(kind), notes: "About it.");

        Assert.Equal("About it.", entry.Notes);
    }

    [Fact]
    public void A_renamed_entry_still_says_what_it_is_about()
        => Assert.Equal(
            "About it.",
            TaskItem.Create("Buy milk", dueDateUtc: null, isCompleted: false, notes: "About it.").WithNewId().Notes);

    /// <summary>
    /// The same room a calendar event's description has, because on a calendar entry this is that
    /// description. Refused rather than cut: a description silently shortened is one somebody has to
    /// notice was shortened.
    /// </summary>
    [Fact]
    public void One_longer_than_the_column_is_refused_rather_than_cut()
        => Assert.Throws<InvalidRequestException>(() => TaskItem.Create(
            "Buy milk", dueDateUtc: null, isCompleted: false,
            notes: new string('a', StoredTextLimits.EventDescription + 1)));

    /// <summary>
    /// A caller that said nothing about it keeps what is stored - the rule that lets the phone and every
    /// older tab go on saving lists without wiping what was typed on the web. See
    /// UpdateTaskListCommand.EntriesKeepingTheirNotes.
    /// </summary>
    [Fact]
    public void An_entry_that_says_nothing_keeps_the_description_it_has()
    {
        var stored = TaskItem.Create("Buy milk", dueDateUtc: null, isCompleted: false, notes: "The blue one.");
        var incoming = TaskItem.Create("Buy milk", dueDateUtc: null, isCompleted: false);

        incoming.KeepNotesOf(stored);

        Assert.Equal("The blue one.", incoming.Notes);
    }

    /// <summary>
    /// All the way through the save that runs every time: an entry sent back without its description
    /// comes out still carrying it, and one sent back with an empty string has cleared it on purpose.
    /// </summary>
    [Theory]
    [InlineData(true, "The blue one.")]
    [InlineData(false, "")]
    public async Task A_save_keeps_or_clears_it_by_whether_the_caller_said_anything(
        bool sayingNothing, string expected)
    {
        var tasks = new InMemoryTaskRepository();
        var entry = TaskItem.Create("Buy milk", dueDateUtc: null, isCompleted: false, notes: "The blue one.");
        var taskList = TaskList.Create(UserId, "Zakupy", [entry]);
        await tasks.AddAsync(taskList, CancellationToken.None);

        var incoming = TaskItem.FromPersistence(
            entry.Id, "Buy milk", dueDateUtc: null, isCompleted: false, linkedTaskListIds: null, reminders: null,
            notes: string.Empty);
        var outcome = await AHandler(tasks).HandleAsync(
            new UpdateTaskListCommand(
                UserId, taskList.Id, "Zakupy", [incoming], IsGroup: false, IsPrivate: false, EncryptedContent: null,
                EntriesKeepingTheirNotes: sayingNothing ? new HashSet<Guid> { entry.Id } : []),
            CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        var stored = await tasks.GetByIdAsync(UserId, taskList.Id, CancellationToken.None);
        Assert.Equal(expected, Assert.Single(stored!.Items).Notes);
    }

    private static UpdateTaskListCommandHandler AHandler(InMemoryTaskRepository tasks)
        => new(
            new TaskListAccessResolver(tasks, new InMemoryTaskListShareRepository(), new InMemoryUserRepository()),
            tasks,
            new TaskListLinkValidator(tasks),
            new RestockCompletion(
                new InMemoryInventoryManagedTaskListRepository(), new InMemoryInventoryItemRepository(),
                new InMemoryInventoryRepository(), new InMemoryTaskRepository()),
            new StockedEntryCompletion(
                new InMemoryInventoryRepository(), new InMemoryInventoryItemRepository()));
}
