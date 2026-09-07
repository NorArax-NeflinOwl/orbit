using Orbit.Core.Abstractions;

namespace Orbit.Core.Folders.GetFolders;

public sealed class GetFoldersQueryHandler : IRequestHandler<GetFoldersQuery, IReadOnlyList<Folder>>
{
    private readonly IFolderRepository _folderRepository;

    public GetFoldersQueryHandler(IFolderRepository folderRepository)
    {
        _folderRepository = folderRepository;
    }

    public Task<IReadOnlyList<Folder>> HandleAsync(GetFoldersQuery request, CancellationToken cancellationToken)
        => _folderRepository.GetAllAsync(request.UserId, cancellationToken);
}
