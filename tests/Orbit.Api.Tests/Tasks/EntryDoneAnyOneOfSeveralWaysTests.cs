using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Orbit.Api.Tests.TestDoubles;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// An entry can be done any one of several ways - "the sauce": buy a ready one, or make it from a list of
/// its own. The other half of an entry standing for several lists (see EntryStandingForSeveralListsTests),
/// and the rule that matters is the opposite one: done as soon as <em>one</em> way is, and a way can be a
/// line of its own, so a single errand needs no list made for it.
/// </summary>
public sealed class EntryDoneAnyOneOfSeveralWaysTests
{
    private readonly LinkedTaskCompletionResolver _resolver = new();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void Ticking_one_line_of_its_own_is_enough()
    {
        var entry = TaskItem.Create(
            "Sauce", null, isCompleted: false,
            alternatives: [new("Buy a ready one", IsDone: true), new("Make it", Guid.NewGuid())]);

        Assert.True(entry.IsCompleted);
    }

    /// <summary>Its tick is its ways': one sent for the entry itself is not believed while none is taken.</summary>
    [Fact]
    public void While_no_way_is_taken_the_entry_is_not_done_whatever_its_own_tick_said()
    {
        var entry = TaskItem.Create(
            "Sauce", null, isCompleted: true, alternatives: [new("Buy a ready one"), new("Make it", Guid.NewGuid())]);

        Assert.False(entry.IsCompleted);
    }

    [Fact]
    public void A_way_that_is_a_list_is_done_when_that_list_is()
    {
        var homemade = TaskList.Create(_userId, "Homemade sauce", [TaskItem.Create("Mayonnaise", null, true)]);
        var burger = TaskList.Create(
            _userId, "Burger",
            [TaskItem.Create("Sauce", null, false, alternatives: [new("Buy a ready one"), new("", homemade.Id)])]);

        var resolved = _resolver.ResolveAll([burger, homemade]);

        var entry = Assert.Single(resolved.Single(list => list.Id == burger.Id).Items);
        Assert.True(entry.IsCompleted);
        Assert.True(entry.Alternatives[1].IsDone);
        Assert.False(entry.Alternatives[0].IsDone);
    }

    [Fact]
    public void And_is_not_done_while_that_list_has_work_left()
    {
        var homemade = TaskList.Create(_userId, "Homemade sauce", [TaskItem.Create("Mayonnaise", null, false)]);
        var burger = TaskList.Create(
            _userId, "Burger",
            [TaskItem.Create("Sauce", null, false, alternatives: [new("Buy a ready one"), new("", homemade.Id)])]);

        var resolved = _resolver.ResolveAll([burger, homemade]);

        Assert.False(Assert.Single(resolved.Single(list => list.Id == burger.Id).Items).IsCompleted);
    }

    /// <summary>A client cannot say a list is done - only the list can, the rule a link has always had.</summary>
    [Fact]
    public void A_way_that_is_a_list_is_never_taken_on_a_clients_word()
    {
        var entry = TaskItem.Create("Sauce", null, false, alternatives: [new("Make it", Guid.NewGuid(), IsDone: true)]);

        Assert.False(entry.Alternatives[0].IsDone);
        Assert.False(entry.IsCompleted);
    }

    /// <summary>
    /// One or the other: "every one of these" and "any one of these" cannot both be what one entry means,
    /// and the links were there first.
    /// </summary>
    [Fact]
    public void An_entry_standing_for_lists_has_no_ways()
    {
        var entry = TaskItem.Create(
            "The flat is ready", null, false, [Guid.NewGuid()], alternatives: [new("Buy a ready one", IsDone: true)]);

        Assert.Empty(entry.Alternatives);
        Assert.False(entry.IsCompleted);
    }

    [Fact]
    public void A_way_with_neither_words_nor_a_list_is_dropped()
    {
        var entry = TaskItem.Create("Sauce", null, false, alternatives: [new("  "), new("Buy a ready one")]);

        Assert.Equal("Buy a ready one", Assert.Single(entry.Alternatives).Description);
    }

    /// <summary>Brought back as work, it keeps its ways and none of them is taken - the choice is made again.</summary>
    [Fact]
    public void Reopening_the_entry_takes_back_the_way_that_was_taken()
    {
        var entry = TaskItem.Create("Sauce", null, false, alternatives: [new("Buy a ready one", IsDone: true)]);

        entry.Reopen();

        Assert.False(entry.IsCompleted);
        Assert.False(Assert.Single(entry.Alternatives).IsDone);
    }

    /// <summary>
    /// Resolving an entry keeps everything else it carries. It used to rebuild a linked entry from its id,
    /// words, date and reminders alone, so a read gave one back without its notes, colour or priority.
    /// </summary>
    [Fact]
    public void Resolving_an_entry_keeps_everything_else_it_carries()
    {
        var kitchen = TaskList.Create(_userId, "Kitchen", [TaskItem.Create("Tiles", null, true)]);
        var flat = TaskList.Create(
            _userId, "Flat",
            [TaskItem.Create(
                "The flat is ready", null, false, [kitchen.Id], notes: "Before the move",
                priority: ItemPriority.High, colour: "#cc4a3f")]);

        var resolved = _resolver.ResolveAll([flat, kitchen]);

        var entry = Assert.Single(resolved.Single(list => list.Id == flat.Id).Items);
        Assert.True(entry.IsCompleted);
        Assert.Equal("Before the move", entry.Notes);
        Assert.Equal(ItemPriority.High, entry.Priority);
        Assert.Equal("#cc4a3f", entry.Colour);
    }

    [Fact]
    public async Task A_way_back_to_its_own_list_is_refused()
    {
        var repository = new InMemoryTaskRepository();
        var burger = TaskList.Create(_userId, "Burger", []);
        await repository.AddAsync(burger, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidRequestException>(() => new TaskListLinkValidator(repository).ValidateAsync(
            _userId, burger.Id,
            [TaskItem.Create("Sauce", null, false, alternatives: [new("", burger.Id)])],
            CancellationToken.None));
    }

    /// <summary>A loop through a way is a loop all the same - the resolver would follow it forever.</summary>
    [Fact]
    public async Task A_cycle_through_a_way_is_refused()
    {
        var repository = new InMemoryTaskRepository();
        var burger = TaskList.Create(_userId, "Burger", []);
        await repository.AddAsync(burger, CancellationToken.None);
        var homemade = TaskList.Create(
            _userId, "Homemade sauce",
            [TaskItem.Create("Back to the burger", null, false, alternatives: [new("", burger.Id)])]);
        await repository.AddAsync(homemade, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidRequestException>(() => new TaskListLinkValidator(repository).ValidateAsync(
            _userId, burger.Id,
            [TaskItem.Create("Sauce", null, false, alternatives: [new("Buy a ready one"), new("", homemade.Id)])],
            CancellationToken.None));
    }

    /// <summary>
    /// A client that knows nothing of ways - the phone builds already installed - saves the entry without
    /// them. It keeps the ways it had, and its tick follows them back rather than following that client.
    /// </summary>
    [Fact]
    public void An_entry_saved_by_a_client_that_knows_nothing_of_ways_keeps_them()
    {
        var stored = TaskItem.Create("Sauce", null, false, alternatives: [new("Buy a ready one", IsDone: true)]);
        var incoming = TaskItem.FromPersistence(stored.Id, "Sauce", null, isCompleted: false, linkedTaskListIds: null, reminders: null);

        incoming.KeepAlternativesOf(stored);

        Assert.Equal("Buy a ready one", Assert.Single(incoming.Alternatives).Description);
        Assert.True(incoming.IsCompleted);
    }
}
