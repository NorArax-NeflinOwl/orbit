using Orbit.Core.Abstractions;

namespace Orbit.Core.Folders.CreateFolder;

/// <summary>
/// Makes a folder for one account. Nothing here refuses a name somebody already used: two tabs called
/// "Work" are a mistake their owner can see and fix, and a rule against them would also refuse the
/// perfectly ordinary case of renaming one to what another used to be called.
/// </summary>
public sealed class CreateFolderCommandHandler : IRequestHandler<CreateFolderCommand, Folder>
{
    private readonly IFolderRepository _folderRepository;

    public CreateFolderCommandHandler(IFolderRepository folderRepository)
    {
        _folderRepository = folderRepository;
    }

    public async Task<Folder> HandleAsync(CreateFolderCommand request, CancellationToken cancellationToken)
    {
        var folder = Folder.Create(request.UserId, request.Name);
        await _folderRepository.AddAsync(folder, cancellationToken);
        return folder;
    }
}
