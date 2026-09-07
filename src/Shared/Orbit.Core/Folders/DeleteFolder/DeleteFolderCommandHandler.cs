using Orbit.Core.Abstractions;

namespace Orbit.Core.Folders.DeleteFolder;

/// <summary>
/// Removes the tab and keeps everything that was under it - see IFolderRepository.DeleteAsync, where
/// emptying it happens. Deleting a folder is not deleting what is in it.
/// </summary>
public sealed class DeleteFolderCommandHandler : IRequestHandler<DeleteFolderCommand, bool>
{
    private readonly IFolderRepository _folderRepository;

    public DeleteFolderCommandHandler(IFolderRepository folderRepository)
    {
        _folderRepository = folderRepository;
    }

    public async Task<bool> HandleAsync(DeleteFolderCommand request, CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(request.UserId, request.FolderId, cancellationToken);
        if (folder is null)
        {
            return false;
        }

        await _folderRepository.DeleteAsync(request.UserId, request.FolderId, cancellationToken);
        return true;
    }
}
