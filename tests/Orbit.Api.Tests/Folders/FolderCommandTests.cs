using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Folders;
using Orbit.Core.Folders.CreateFolder;
using Orbit.Core.Folders.DeleteFolder;
using Orbit.Core.Folders.RenameFolder;
using Xunit;

namespace Orbit.Api.Tests.Folders;

/// <summary>
/// Making, renaming and removing the tabs the pages made of cards are read under. The two built-in
/// folders are not here and never will be: they have no rows, and which one something is in follows
/// from what it already is - see <see cref="BuiltInFolder"/>.
/// </summary>
public sealed class FolderCommandTests
{
    private readonly InMemoryFolderRepository _folders = new();
    private static readonly Guid OwnerUserId = Guid.NewGuid();

    [Fact]
    public async Task A_folder_is_made_for_the_account_that_asked_for_it()
    {
        var folder = await new CreateFolderCommandHandler(_folders).HandleAsync(
            new CreateFolderCommand(OwnerUserId, "Work", FolderScope.Tasks), CancellationToken.None);

        Assert.Equal("Work", folder.Name);
        Assert.Equal(OwnerUserId, folder.UserId);
        Assert.NotNull(await _folders.GetByIdAsync(OwnerUserId, folder.Id, CancellationToken.None));
    }

    /// <summary>A tab has to say something - a nameless one is a gap nobody can tell from a fault.</summary>
    [Fact]
    public async Task A_folder_with_nothing_but_spaces_for_a_name_is_refused()
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() => new CreateFolderCommandHandler(_folders).HandleAsync(
            new CreateFolderCommand(OwnerUserId, "   ", FolderScope.Tasks), CancellationToken.None));
    }

    [Fact]
    public async Task A_name_is_stored_trimmed_so_two_tabs_cannot_look_alike()
    {
        var folder = await new CreateFolderCommandHandler(_folders).HandleAsync(
            new CreateFolderCommand(OwnerUserId, "  Work  ", FolderScope.Tasks), CancellationToken.None);

        Assert.Equal("Work", folder.Name);
    }

    [Fact]
    public async Task Renaming_says_no_when_the_folder_is_somebody_elses()
    {
        var folder = await new CreateFolderCommandHandler(_folders).HandleAsync(
            new CreateFolderCommand(OwnerUserId, "Work", FolderScope.Tasks), CancellationToken.None);

        var renamed = await new RenameFolderCommandHandler(_folders).HandleAsync(
            new RenameFolderCommand(Guid.NewGuid(), folder.Id, "Mine now"), CancellationToken.None);

        Assert.False(renamed);
        Assert.Equal("Work", (await _folders.GetByIdAsync(OwnerUserId, folder.Id, CancellationToken.None))!.Name);
    }

    [Fact]
    public async Task Deleting_one_that_is_not_there_says_so_rather_than_throwing()
    {
        var deleted = await new DeleteFolderCommandHandler(_folders).HandleAsync(
            new DeleteFolderCommand(OwnerUserId, Guid.NewGuid()), CancellationToken.None);

        Assert.False(deleted);
    }
}
