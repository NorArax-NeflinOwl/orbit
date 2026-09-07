using Microsoft.EntityFrameworkCore;
using Orbit.Core.Folders;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class FolderRepository : IFolderRepository
{
    private readonly OrbitDbContext _dbContext;

    public FolderRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Folder>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.Folders
            .AsNoTracking()
            .Where(folder => folder.UserId == userId)
            .OrderBy(folder => folder.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return [.. entities.Select(ToDomain)];
    }

    public async Task<Folder?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Folders
            .AsNoTracking()
            .FirstOrDefaultAsync(folder => folder.Id == id && folder.UserId == userId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(Folder folder, CancellationToken cancellationToken)
    {
        _dbContext.Folders.Add(ToEntity(folder));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Folder folder, CancellationToken cancellationToken)
    {
        _dbContext.Folders.Update(ToEntity(folder));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Empties the folder and then removes it, in one save. The emptying is done here rather than left
    /// to a foreign key so it cannot be anything but this: notes and lists filed under it go back to
    /// having no folder, which is the built-in one their own privacy decides. A cascade would have
    /// deleted them with the tab, and SET NULL would have done the right thing silently - this says it
    /// out loud, and is the same work either way.
    /// </summary>
    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Folders
            .FirstOrDefaultAsync(folder => folder.Id == id && folder.UserId == userId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        await _dbContext.Notes
            .Where(note => note.UserId == userId && note.FolderId == id)
            .ExecuteUpdateAsync(note => note.SetProperty(stored => stored.FolderId, (Guid?)null), cancellationToken);
        await _dbContext.Tasks
            .Where(taskList => taskList.UserId == userId && taskList.FolderId == id)
            .ExecuteUpdateAsync(taskList => taskList.SetProperty(stored => stored.FolderId, (Guid?)null), cancellationToken);

        _dbContext.Folders.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Folder ToDomain(FolderEntity entity)
        => Folder.FromPersistence(entity.Id, entity.UserId, entity.Name, entity.CreatedAtUtc, entity.UpdatedAtUtc);

    private static FolderEntity ToEntity(Folder folder)
        => new()
        {
            Id = folder.Id,
            UserId = folder.UserId,
            Name = folder.Name,
            CreatedAtUtc = folder.CreatedAtUtc,
            UpdatedAtUtc = folder.UpdatedAtUtc
        };
}
