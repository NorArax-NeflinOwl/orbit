using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Tags;
using Orbit.Core.Tags.SetTagColour;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Tags;

/// <summary>
/// Tags on notes and task lists, and the colour an account gives each - see Orbit.Core.Tags. The rules
/// that matter are the ones a client leans on without checking: a word written twice is one tag, a save
/// that says nothing about tags keeps them, a private item keeps none readable, and a colour belongs to
/// the word whatever case it is written in.
/// </summary>
public sealed class TagTests
{
    private static readonly EncryptedPayload Sealed = new("c2VhbGVk", "bm9uY2U=");

    [Fact]
    public void A_notes_tags_are_tidied_as_they_are_given()
    {
        var note = Note.Create(Guid.NewGuid(), "Groceries", [], tags: [" work ", "Work", "", "home"]);

        Assert.Equal(["work", "home"], note.Tags);
    }

    /// <summary>
    /// Null is what an installed phone sends - it has never heard of tags - and a save from it must not
    /// untag the note. An empty list is somebody taking every tag off.
    /// </summary>
    [Fact]
    public void A_note_saved_without_a_word_about_tags_keeps_them_and_an_empty_list_clears_them()
    {
        var note = Note.Create(Guid.NewGuid(), "Groceries", [], tags: ["work"]);

        note.Update("Groceries", [], isPrivate: false, encryptedContent: null, ItemPriority.Normal, tags: null);
        Assert.Equal(["work"], note.Tags);

        note.Update("Groceries", [], isPrivate: false, encryptedContent: null, ItemPriority.Normal, tags: []);
        Assert.Empty(note.Tags);
    }

    /// <summary>A private note's tags are sealed with it, so the server keeps none it could read.</summary>
    [Fact]
    public void A_private_note_keeps_no_readable_tag()
    {
        var created = Note.Create(Guid.NewGuid(), string.Empty, [], isPrivate: true, encryptedContent: Sealed, tags: ["work"]);
        var updated = Note.Create(Guid.NewGuid(), "Groceries", [], tags: ["work"]);

        updated.Update(string.Empty, [], isPrivate: true, Sealed, ItemPriority.Normal, tags: ["work"]);

        Assert.Empty(created.Tags);
        Assert.Empty(updated.Tags);
    }

    [Fact]
    public void A_task_list_keeps_its_tags_through_a_save_that_says_nothing_and_none_when_private()
    {
        var taskList = TaskList.Create(Guid.NewGuid(), "Errands", [], tags: ["work", "WORK", "weekly"]);
        Assert.Equal(["work", "weekly"], taskList.Tags);

        taskList.Update("Errands", [], isGroup: false, isPrivate: false, encryptedContent: null, ItemPriority.Normal);
        Assert.Equal(["work", "weekly"], taskList.Tags);

        taskList.Update(string.Empty, [], isGroup: false, isPrivate: true, Sealed, ItemPriority.Normal, tags: ["work"]);
        Assert.Empty(taskList.Tags);
    }

    /// <summary>"Work" and "work" are one tag, so they have one colour - the one set last.</summary>
    [Fact]
    public async Task A_colour_belongs_to_the_tag_whatever_its_case()
    {
        var handler = new SetTagColourCommandHandler(new InMemoryTagColourRepository());
        var userId = Guid.NewGuid();

        await handler.HandleAsync(new SetTagColourCommand(userId, "Work", "#AA3355"), CancellationToken.None);
        var colours = await handler.HandleAsync(new SetTagColourCommand(userId, "work", "#113355"), CancellationToken.None);

        Assert.Equal(new TagColour("work", "#113355"), Assert.Single(colours));
    }

    [Fact]
    public async Task An_empty_colour_takes_the_tags_colour_away()
    {
        var handler = new SetTagColourCommandHandler(new InMemoryTagColourRepository());
        var userId = Guid.NewGuid();
        await handler.HandleAsync(new SetTagColourCommand(userId, "work", "#aa3355"), CancellationToken.None);

        var colours = await handler.HandleAsync(new SetTagColourCommand(userId, "Work", string.Empty), CancellationToken.None);

        Assert.Empty(colours);
    }

    /// <summary>A colour is what a colour input gives, and nothing a card could be made to draw instead.</summary>
    [Theory]
    [InlineData("red")]
    [InlineData("#abc")]
    [InlineData("#aa3355; background: url(x)")]
    public async Task A_colour_that_is_not_hex_is_refused(string colour)
    {
        var handler = new SetTagColourCommandHandler(new InMemoryTagColourRepository());

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => handler.HandleAsync(new SetTagColourCommand(Guid.NewGuid(), "work", colour), CancellationToken.None));
    }

    [Fact]
    public async Task A_tag_with_no_name_has_nothing_to_colour()
    {
        var handler = new SetTagColourCommandHandler(new InMemoryTagColourRepository());

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => handler.HandleAsync(new SetTagColourCommand(Guid.NewGuid(), "  ", "#aa3355"), CancellationToken.None));
    }

    /// <summary>One account's colours are its own: another account's "work" is drawn however they chose.</summary>
    [Fact]
    public async Task Colours_are_one_accounts_own()
    {
        var repository = new InMemoryTagColourRepository();
        var handler = new SetTagColourCommandHandler(repository);
        await handler.HandleAsync(new SetTagColourCommand(Guid.NewGuid(), "work", "#aa3355"), CancellationToken.None);

        Assert.Empty(await repository.GetAllAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
