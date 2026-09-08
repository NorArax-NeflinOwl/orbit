using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.CopyTaskItem;
using Orbit.Core.Tasks.MoveTaskItem;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// The way round a move that cannot happen. An entry on a list somebody shared belongs to that list and
/// travels with it, so taking it out would take it from everybody else the list was shared with - the
/// move says so, and a copy is what it offers instead.
/// </summary>
public sealed class CopyTaskItemCommandHandlerTests
{
    private readonly InMemoryTaskRepository _taskLists = new();
    private readonly InMemoryTaskListShareRepository _shares = new();
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly Guid RecipientUserId = Guid.NewGuid();

    private TaskListAccessResolver Resolver
        => new(_taskLists, _shares, new InMemoryUserRepository());

    /// <summary>
    /// The refusal this whole feature hangs off: both lists resolve, both can be edited, and the entry
    /// still cannot move - said as a refusal with a reason rather than as "no such list", which is what
    /// it used to answer for something plainly on the reader's own screen.
    /// </summary>
    [Fact]
    public async Task Moving_an_entry_off_a_shared_list_is_refused_and_says_why()
    {
        var (sharedList, item) = await ASharedListHoldingAnEntryAsync();
        var myOwnList = TaskList.Create(RecipientUserId, "Mine", []);
        await _taskLists.AddAsync(myOwnList, CancellationToken.None);

        var outcome = await new MoveTaskItemCommandHandler(Resolver, _taskLists, new TaskListLinkValidator(_taskLists))
            .HandleAsync(new MoveTaskItemCommand(RecipientUserId, sharedList.Id, item.Id, myOwnList.Id), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Refused, outcome.Kind);
        Assert.Contains("shared along with the whole list", outcome.Reason);
    }

    [Fact]
    public async Task Copying_writes_a_second_entry_and_leaves_the_first_where_it_was()
    {
        var (sharedList, item) = await ASharedListHoldingAnEntryAsync();
        var myOwnList = TaskList.Create(RecipientUserId, "Mine", []);
        await _taskLists.AddAsync(myOwnList, CancellationToken.None);

        var outcome = await new CopyTaskItemCommandHandler(Resolver, _taskLists).HandleAsync(
            new CopyTaskItemCommand(RecipientUserId, sharedList.Id, item.Id, myOwnList.Id), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        var copy = Assert.Single((await _taskLists.GetByIdAsync(RecipientUserId, myOwnList.Id, CancellationToken.None))!.Items);
        Assert.Equal("Buy milk", copy.Description);
        Assert.Single((await _taskLists.GetByIdAsync(OwnerUserId, sharedList.Id, CancellationToken.None))!.Items);
    }

    /// <summary>
    /// Everything the entry itself says comes across - that is what makes the copy a way round the move
    /// rather than a new blank entry to fill in again.
    /// </summary>
    [Fact]
    public async Task The_copy_carries_every_field_the_entry_had()
    {
        var due = DateTimeOffset.UtcNow.AddDays(3);
        var item = TaskItem.Create(
            "Buy milk", due, isCompleted: true, linkedTaskListIds: null,
            new TaskItemReminders(NotificationChannelForTests, Daily: true, NotificationChannelForTests, new TimeOnly(8, 30)),
            new TaskItemSubject(TaskItemKind.Calendar, "Wały Piastowskie 1, Gdańsk"),
            ["shopping", "home"],
            product: null,
            notes: "The one in the glass bottle");
        var (sharedList, _) = await ASharedListHoldingAsync(item);
        var myOwnList = TaskList.Create(RecipientUserId, "Mine", []);
        await _taskLists.AddAsync(myOwnList, CancellationToken.None);

        await new CopyTaskItemCommandHandler(Resolver, _taskLists).HandleAsync(
            new CopyTaskItemCommand(RecipientUserId, sharedList.Id, item.Id, myOwnList.Id), CancellationToken.None);

        var copy = Assert.Single((await _taskLists.GetByIdAsync(RecipientUserId, myOwnList.Id, CancellationToken.None))!.Items);
        Assert.NotEqual(item.Id, copy.Id);
        Assert.Equal("Buy milk", copy.Description);
        Assert.Equal("The one in the glass bottle", copy.Notes);
        Assert.Equal(due, copy.DueDateUtc);
        Assert.True(copy.IsCompleted);
        Assert.Equal(TaskItemKind.Calendar, copy.Kind);
        Assert.Equal("Wały Piastowskie 1, Gdańsk", copy.Location);
        Assert.Equal(["shopping", "home"], copy.Categories);
        Assert.True(copy.RemindDaily);
        Assert.Equal(new TimeOnly(8, 30), copy.DailyReminderTimeOfDay);
    }

    /// <summary>
    /// The one thing a copy deliberately drops. The appointment belongs to the account the list came
    /// from, so a copy pointing at it would hand this reader a link to something they cannot open - and
    /// two entries on one appointment is the drift the link exists to prevent. What the entry says about
    /// the place is text, and that does come across (see the test above).
    /// </summary>
    [Fact]
    public async Task The_copy_does_not_point_at_the_original_appointment()
    {
        var item = TaskItem.Create(
            "Dentist", null, isCompleted: false, linkedTaskListIds: null, reminders: null,
            new TaskItemSubject(TaskItemKind.Calendar, "Długa 4", linkedCalendarEventId: Guid.NewGuid()));
        var (sharedList, _) = await ASharedListHoldingAsync(item);
        var myOwnList = TaskList.Create(RecipientUserId, "Mine", []);
        await _taskLists.AddAsync(myOwnList, CancellationToken.None);

        await new CopyTaskItemCommandHandler(Resolver, _taskLists).HandleAsync(
            new CopyTaskItemCommand(RecipientUserId, sharedList.Id, item.Id, myOwnList.Id), CancellationToken.None);

        var copy = Assert.Single((await _taskLists.GetByIdAsync(RecipientUserId, myOwnList.Id, CancellationToken.None))!.Items);
        Assert.Null(copy.LinkedCalendarEventId);
    }

    /// <summary>Nothing is copied onto a list this reader has no say over.</summary>
    [Fact]
    public async Task Copying_into_a_list_that_is_not_this_readers_to_edit_says_nothing_is_there()
    {
        var (sharedList, item) = await ASharedListHoldingAnEntryAsync();
        var strangerUserId = Guid.NewGuid();
        var strangersList = TaskList.Create(strangerUserId, "Theirs", []);
        await _taskLists.AddAsync(strangersList, CancellationToken.None);

        var outcome = await new CopyTaskItemCommandHandler(Resolver, _taskLists).HandleAsync(
            new CopyTaskItemCommand(RecipientUserId, sharedList.Id, item.Id, strangersList.Id), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
        // Asked as its owner, so the list really is read back rather than coming up null for the
        // wrong user and passing on an empty answer.
        Assert.Empty((await _taskLists.GetByIdAsync(strangerUserId, strangersList.Id, CancellationToken.None))!.Items);
    }

    /// <summary>
    /// A list shared read-only can still be copied out of: nothing about it changes, and the reason a
    /// move is refused - the entry leaving a list other people hold - does not apply.
    /// </summary>
    [Fact]
    public async Task An_entry_on_a_read_only_share_can_still_be_copied()
    {
        var (sharedList, item) = await ASharedListHoldingAnEntryAsync(ShareAccessLevel.ReadOnly);
        var myOwnList = TaskList.Create(RecipientUserId, "Mine", []);
        await _taskLists.AddAsync(myOwnList, CancellationToken.None);

        var outcome = await new CopyTaskItemCommandHandler(Resolver, _taskLists).HandleAsync(
            new CopyTaskItemCommand(RecipientUserId, sharedList.Id, item.Id, myOwnList.Id), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
    }

    private const Orbit.Core.Notifications.NotificationChannel NotificationChannelForTests
        = Orbit.Core.Notifications.NotificationChannel.Push;

    private async Task<(TaskList SharedList, TaskItem Item)> ASharedListHoldingAnEntryAsync(
        ShareAccessLevel accessLevel = ShareAccessLevel.CanEdit)
        => await ASharedListHoldingAsync(TaskItem.Create("Buy milk", null, false), accessLevel);

    /// <summary>Somebody else's list, accepted by this reader - which is the whole situation being tested.</summary>
    private async Task<(TaskList SharedList, TaskItem Item)> ASharedListHoldingAsync(
        TaskItem item, ShareAccessLevel accessLevel = ShareAccessLevel.CanEdit)
    {
        var sharedList = TaskList.Create(OwnerUserId, "Theirs", [item]);
        await _taskLists.AddAsync(sharedList, CancellationToken.None);
        var share = TaskListShare.Create(sharedList.Id, OwnerUserId, RecipientUserId, accessLevel);
        share.MarkAccepted();
        await _shares.AddAsync(share, CancellationToken.None);
        return (sharedList, item);
    }
}
