using Orbit.Core.Folders;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IFolderRepository"/> for unit tests. Empties nothing on delete - the real one
/// unfiles what was in the folder, which is a query across two other tables and belongs to a test with
/// a database in it (see FolderRepositoryTests).
/// </summary>
internal sealed class InMemoryFolderRepository : IFolderRepository
{
    private readonly List<Folder> _folders = [];

    public Task<IReadOnlyList<Folder>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Folder>>([.. _folders.Where(folder => folder.UserId == userId)]);

    public Task<Folder?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_folders.FirstOrDefault(folder => folder.Id == id && folder.UserId == userId));

    public Task AddAsync(Folder folder, CancellationToken cancellationToken)
    {
        _folders.Add(folder);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Folder folder, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        _folders.RemoveAll(folder => folder.Id == id && folder.UserId == userId);
        return Task.CompletedTask;
    }
}
