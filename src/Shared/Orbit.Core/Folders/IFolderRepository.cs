namespace Orbit.Core.Folders;

public interface IFolderRepository
{
    /// <summary>Everything this user has made, oldest first - which is the order their tabs are drawn in.</summary>
    Task<IReadOnlyList<Folder>> GetAllAsync(Guid userId, CancellationToken cancellationToken);

    Task<Folder?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(Folder folder, CancellationToken cancellationToken);

    Task UpdateAsync(Folder folder, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the folder and empties it - everything filed in it goes back to whichever built-in folder
    /// it belongs to, which is what its own privacy already says (see <see cref="BuiltInFolder"/>).
    /// Deleting a folder is not deleting what is in it, and a note that vanished with the tab it was
    /// under would be the worst possible reading of a tidy-up.
    /// </summary>
    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}
