using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Orbit.Api.Tests.TestDoubles;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// An entry can stand for more than one list. "The flat is ready" means the kitchen and the bathroom
/// and the hall, and writing that as three entries saying the same thing loses that it is one step.
///
/// The rule that matters is when such an entry counts as done, and since 2026-09-14 the entry says which
/// it means: <b>any one of the lists it names by default</b>, every one of them when it is asked to
/// (TaskItem.NeedsEveryLinkedList). "Buy a cake" stands for baking one and going to the baker and either
/// finishes it; "the flat is ready" stands for the kitchen and the bathroom and needs both.
/// </summary>
public sealed class EntryStandingForSeveralListsTests
{
    private readonly LinkedTaskCompletionResolver _resolver = new();
    private readonly Guid _userId = Guid.NewGuid();

    /// <summary>The default: one of the two is enough, which is what writing one entry for two ways means.</summary>
    [Fact]
    public void An_entry_standing_for_two_lists_is_done_as_soon_as_either_is()
    {
        var baking = TaskList.Create(_userId, "Bake one", [TaskItem.Create("Flour", null, true)]);
        var buying = TaskList.Create(_userId, "Go to the baker", [TaskItem.Create("Queue", null, false)]);
        var party = TaskList.Create(
            _userId, "Party", [TaskItem.Create("A cake", null, false, [baking.Id, buying.Id])]);

        var resolved = _resolver.ResolveAll([party, baking, buying]);

        var entry = Assert.Single(resolved.Single(list => list.Id == party.Id).Items);
        Assert.True(entry.IsCompleted);
    }

    /// <summary>And not before one of them is.</summary>
    [Fact]
    public void An_entry_standing_for_two_lists_is_not_done_while_neither_is()
    {
        var baking = TaskList.Create(_userId, "Bake one", [TaskItem.Create("Flour", null, false)]);
        var buying = TaskList.Create(_userId, "Go to the baker", [TaskItem.Create("Queue", null, false)]);
        var party = TaskList.Create(
            _userId, "Party", [TaskItem.Create("A cake", null, false, [baking.Id, buying.Id])]);

        var resolved = _resolver.ResolveAll([party, baking, buying]);

        var entry = Assert.Single(resolved.Single(list => list.Id == party.Id).Items);
        Assert.False(entry.IsCompleted);
    }

    [Fact]
    public void An_entry_asked_for_all_of_them_is_not_done_while_either_has_work_left()
    {
        var kitchen = TaskList.Create(_userId, "Kitchen", [TaskItem.Create("Tiles", null, true)]);
        var bathroom = TaskList.Create(_userId, "Bathroom", [TaskItem.Create("Grout", null, false)]);
        var flat = TaskList.Create(
            _userId, "Flat",
            [TaskItem.Create("The flat is ready", null, false, [kitchen.Id, bathroom.Id], needsEveryLinkedList: true)]);

        var resolved = _resolver.ResolveAll([flat, kitchen, bathroom]);

        var entry = Assert.Single(resolved.Single(list => list.Id == flat.Id).Items);
        Assert.False(entry.IsCompleted);
    }

    [Fact]
    public void And_is_done_once_both_are()
    {
        var kitchen = TaskList.Create(_userId, "Kitchen", [TaskItem.Create("Tiles", null, true)]);
        var bathroom = TaskList.Create(_userId, "Bathroom", [TaskItem.Create("Grout", null, true)]);
        var flat = TaskList.Create(
            _userId, "Flat",
            [TaskItem.Create("The flat is ready", null, false, [kitchen.Id, bathroom.Id], needsEveryLinkedList: true)]);

        var resolved = _resolver.ResolveAll([flat, kitchen, bathroom]);

        var entry = Assert.Single(resolved.Single(list => list.Id == flat.Id).Items);
        Assert.True(entry.IsCompleted);
    }

    /// <summary>
    /// An entry that said nothing about the rule keeps the one it has - the eighth field to follow that
    /// rule, and the phone is the reason: it saves lists without knowing the rule can be either, and
    /// such a save must not turn an entry somebody set to "all of them" back to the default.
    /// </summary>
    [Fact]
    public void An_entry_saved_by_a_client_that_knows_nothing_of_the_rule_keeps_it()
    {
        var stored = TaskItem.Create(
            "The flat is ready", null, false, [Guid.NewGuid(), Guid.NewGuid()], needsEveryLinkedList: true);
        var incoming = TaskItem.FromPersistence(
            stored.Id, "The flat is ready", null, isCompleted: false,
            linkedTaskListIds: stored.LinkedTaskListIds, reminders: null);

        incoming.KeepListRuleOf(stored);

        Assert.True(incoming.NeedsEveryLinkedList);
    }

    /// <summary>
    /// A list that is not there resolves to "not done", the same as a single missing link always did -
    /// so one unreachable list holds the whole entry open rather than being quietly skipped.
    /// </summary>
    [Fact]
    public void A_list_that_cannot_be_found_holds_the_entry_open()
    {
        var kitchen = TaskList.Create(_userId, "Kitchen", [TaskItem.Create("Tiles", null, true)]);
        var flat = TaskList.Create(
            _userId, "Flat",
            [TaskItem.Create("The flat is ready", null, false, [kitchen.Id, Guid.NewGuid()], needsEveryLinkedList: true)]);

        var resolved = _resolver.ResolveAll([flat, kitchen]);

        Assert.False(Assert.Single(resolved.Single(list => list.Id == flat.Id).Items).IsCompleted);
    }

    /// <summary>Naming the same list twice is one link written twice, not two steps.</summary>
    [Fact]
    public void The_same_list_named_twice_is_kept_once()
    {
        var kitchen = TaskList.Create(_userId, "Kitchen", []);

        var entry = TaskItem.Create("The flat is ready", null, false, [kitchen.Id, kitchen.Id]);

        Assert.Equal([kitchen.Id], entry.LinkedTaskListIds);
    }

    [Fact]
    public void An_entry_naming_no_lists_is_work_of_its_own()
    {
        var entry = TaskItem.Create("Buy milk", null, false);

        Assert.False(entry.IsALinkToOtherLists);
        Assert.Empty(entry.LinkedTaskListIds);
    }

    /// <summary>
    /// A loop through the second link is a loop all the same. Without this the completion walk would
    /// follow it forever - which is why the rule exists at all rather than being a matter of taste.
    /// </summary>
    [Fact]
    public async Task A_cycle_through_the_second_list_is_refused()
    {
        var repository = new InMemoryTaskRepository();
        var flat = TaskList.Create(_userId, "Flat", []);
        await repository.AddAsync(flat, CancellationToken.None);
        var kitchen = TaskList.Create(_userId, "Kitchen", []);
        await repository.AddAsync(kitchen, CancellationToken.None);
        var bathroom = TaskList.Create(
            _userId, "Bathroom", [TaskItem.Create("Back to the flat", null, false, [flat.Id])]);
        await repository.AddAsync(bathroom, CancellationToken.None);

        var validator = new TaskListLinkValidator(repository);

        // The first link is harmless; the second closes the loop.
        await Assert.ThrowsAsync<InvalidRequestException>(() => validator.ValidateAsync(
            _userId,
            flat.Id,
            [TaskItem.Create("The flat is ready", null, false, [kitchen.Id, bathroom.Id])],
            CancellationToken.None));
    }
}
