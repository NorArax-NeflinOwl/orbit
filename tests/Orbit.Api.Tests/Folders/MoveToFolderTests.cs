using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Folders;
using Orbit.Core.Folders.CreateFolder;
using Orbit.Core.Notes;
using Orbit.Core.Notes.MoveNoteToFolder;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.MoveTaskListToFolder;
using Xunit;

namespace Orbit.Api.Tests.Folders;

/// <summary>
/// Filing a note or a list. Its own command rather than a field on the update - see
/// MoveNoteToFolderCommand - and the two checks that keep a card where its owner can find it.
/// </summary>
public sealed class MoveToFolderTests
{
    private readonly InMemoryFolderRepository _folders = new();
    private readonly InMemoryNoteRepository _notes = new();
    private readonly InMemoryTaskRepository _taskLists = new();
    private static readonly Guid OwnerUserId = Guid.NewGuid();

    [Fact]
    public async Task A_note_is_filed_under_the_folder_it_was_sent_to()
    {
        var folder = await AFolderCalled("Work");
        var note = Note.Create(OwnerUserId, "Shopping", [NoteContentLine.PlainText("Milk")]);
        await _notes.AddAsync(note, CancellationToken.None);

        var moved = await new MoveNoteToFolderCommandHandler(_notes, _folders).HandleAsync(
            new MoveNoteToFolderCommand(OwnerUserId, note.Id, folder.Id), CancellationToken.None);

        Assert.True(moved);
        Assert.Equal(folder.Id, note.FolderId);
    }

    /// <summary>
    /// Null is how something is taken out of a folder, and it lands back in the built-in one its own
    /// privacy decides - see BuiltInFolder. It is not "leave it alone", which is why filing travels on
    /// its own request instead of on the update.
    /// </summary>
    [Fact]
    public async Task Filing_a_note_under_nothing_takes_it_out_of_the_folder()
    {
        var folder = await AFolderCalled("Work");
        var note = Note.Create(OwnerUserId, "Shopping", [NoteContentLine.PlainText("Milk")], folderId: folder.Id);
        await _notes.AddAsync(note, CancellationToken.None);

        await new MoveNoteToFolderCommandHandler(_notes, _folders).HandleAsync(
            new MoveNoteToFolderCommand(OwnerUserId, note.Id, FolderId: null), CancellationToken.None);

        Assert.Null(note.FolderId);
    }

    /// <summary>
    /// A folder id arrives from a client, so one belonging to somebody else has to be refused: filing a
    /// note under a tab its owner cannot see is the same thing as losing it.
    /// </summary>
    [Fact]
    public async Task A_note_is_not_filed_under_somebody_elses_folder()
    {
        var theirFolder = await new CreateFolderCommandHandler(_folders).HandleAsync(
            new CreateFolderCommand(Guid.NewGuid(), "Theirs", FolderScope.Notes), CancellationToken.None);
        var note = Note.Create(OwnerUserId, "Shopping", [NoteContentLine.PlainText("Milk")]);
        await _notes.AddAsync(note, CancellationToken.None);

        var moved = await new MoveNoteToFolderCommandHandler(_notes, _folders).HandleAsync(
            new MoveNoteToFolderCommand(OwnerUserId, note.Id, theirFolder.Id), CancellationToken.None);

        Assert.False(moved);
        Assert.Null(note.FolderId);
    }

    [Fact]
    public async Task A_task_list_is_filed_the_same_way()
    {
        var folder = await AFolderCalled("Work");
        var taskList = TaskList.Create(OwnerUserId, "Moving", []);
        await _taskLists.AddAsync(taskList, CancellationToken.None);

        var moved = await new MoveTaskListToFolderCommandHandler(_taskLists, _folders).HandleAsync(
            new MoveTaskListToFolderCommand(OwnerUserId, taskList.Id, folder.Id), CancellationToken.None);

        Assert.True(moved);
        Assert.Equal(folder.Id, taskList.FolderId);
    }

    [Fact]
    public async Task A_list_that_is_not_this_readers_is_not_theirs_to_file()
    {
        var folder = await AFolderCalled("Work");
        var taskList = TaskList.Create(Guid.NewGuid(), "Theirs", []);
        await _taskLists.AddAsync(taskList, CancellationToken.None);

        var moved = await new MoveTaskListToFolderCommandHandler(_taskLists, _folders).HandleAsync(
            new MoveTaskListToFolderCommand(OwnerUserId, taskList.Id, folder.Id), CancellationToken.None);

        Assert.False(moved);
        Assert.Null(taskList.FolderId);
    }

    private Task<Folder> AFolderCalled(string name)
        => new CreateFolderCommandHandler(_folders).HandleAsync(
            new CreateFolderCommand(OwnerUserId, name, FolderScope.Notes), CancellationToken.None);
}
