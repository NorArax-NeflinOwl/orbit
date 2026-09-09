using Microsoft.EntityFrameworkCore;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Folders;
using Orbit.Data;
using Orbit.Data.Entities;
using Orbit.Data.Repositories;
using Xunit;

namespace Orbit.Api.Tests.Folders;

/// <summary>
/// The real <see cref="FolderRepository"/> against a database rather than the in-memory double, because
/// the part worth proving is the part a double replaces: deleting a folder has to empty it, and a
/// mistake there is the difference between tidying up a tab and losing everything under it.
///
/// SQLite stands in for PostgreSQL as it does in AccountDeletionSweepTests - what is being tested is
/// which rows the method decides to touch, not anything provider-specific.
/// </summary>
public sealed class FolderRepositoryTests : IDisposable
{
    private readonly TemporarySqliteDatabase _database = new();
    private readonly OrbitDbContext _dbContext;
    private static readonly Guid OwnerUserId = Guid.NewGuid();

    public FolderRepositoryTests() => _dbContext = _database.DbContext;

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Deleting_a_folder_keeps_what_was_in_it_and_only_unfiles_it()
    {
        var repository = new FolderRepository(_dbContext);
        var folder = Folder.Create(OwnerUserId, "Work", FolderScope.Tasks);
        await repository.AddAsync(folder, CancellationToken.None);
        var noteId = await ANoteFiledUnderAsync(folder.Id);
        var taskListId = AListFiledUnder(folder.Id);
        await _dbContext.SaveChangesAsync();

        await repository.DeleteAsync(OwnerUserId, folder.Id, CancellationToken.None);

        Assert.Null(await repository.GetByIdAsync(OwnerUserId, folder.Id, CancellationToken.None));
        var note = await _dbContext.Notes.AsNoTracking().FirstAsync(stored => stored.Id == noteId);
        var taskList = await _dbContext.Tasks.AsNoTracking().FirstAsync(stored => stored.Id == taskListId);
        Assert.Null(note.FolderId);
        Assert.Null(taskList.FolderId);
    }

    /// <summary>
    /// Only this folder's contents. A tab being removed says nothing about what is in any other one, and
    /// an ExecuteUpdate that forgot its Where would empty every folder the account has.
    /// </summary>
    [Fact]
    public async Task Deleting_one_folder_leaves_what_is_filed_in_another_alone()
    {
        var repository = new FolderRepository(_dbContext);
        var goingAway = Folder.Create(OwnerUserId, "Work", FolderScope.Tasks);
        var staying = Folder.Create(OwnerUserId, "Home", FolderScope.Tasks);
        await repository.AddAsync(goingAway, CancellationToken.None);
        await repository.AddAsync(staying, CancellationToken.None);
        var noteId = await ANoteFiledUnderAsync(staying.Id);
        await _dbContext.SaveChangesAsync();

        await repository.DeleteAsync(OwnerUserId, goingAway.Id, CancellationToken.None);

        var note = await _dbContext.Notes.AsNoTracking().FirstAsync(stored => stored.Id == noteId);
        Assert.Equal(staying.Id, note.FolderId);
    }

    /// <summary>Somebody else's folder is not there to be deleted, the same as one that never existed.</summary>
    [Fact]
    public async Task A_folder_belonging_to_somebody_else_is_not_deleted()
    {
        var repository = new FolderRepository(_dbContext);
        var theirs = Folder.Create(Guid.NewGuid(), "Theirs", FolderScope.Tasks);
        await repository.AddAsync(theirs, CancellationToken.None);

        await repository.DeleteAsync(OwnerUserId, theirs.Id, CancellationToken.None);

        Assert.Single(await _dbContext.Folders.AsNoTracking().ToListAsync());
    }

    private async Task<Guid> ANoteFiledUnderAsync(Guid folderId)
    {
        var noteId = Guid.NewGuid();
        _dbContext.Notes.Add(new NoteEntity
        {
            Id = noteId,
            UserId = OwnerUserId,
            Title = "Shopping",
            ContentJson = "[]",
            FolderId = folderId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await Task.CompletedTask;
        return noteId;
    }

    private Guid AListFiledUnder(Guid folderId)
    {
        var taskListId = Guid.NewGuid();
        _dbContext.Tasks.Add(new TaskEntity
        {
            Id = taskListId,
            UserId = OwnerUserId,
            Title = "Moving",
            FolderId = folderId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        return taskListId;
    }
}
