using Orbit.Core.Abstractions;

namespace Orbit.Core.Folders.RenameFolder;

/// <summary>False when there is no such folder of this user's - which is also what somebody else's looks like.</summary>
public sealed class RenameFolderCommandHandler : IRequestHandler<RenameFolderCommand, bool>
{
    private readonly IFolderRepository _folderRepository;

    public RenameFolderCommandHandler(IFolderRepository folderRepository)
    {
        _folderRepository = folderRepository;
    }

    public async Task<bool> HandleAsync(RenameFolderCommand request, CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(request.UserId, request.FolderId, cancellationToken);
        if (folder is null)
        {
            return false;
        }

        folder.Rename(request.Name);
        await _folderRepository.UpdateAsync(folder, cancellationToken);
        return true;
    }
}
