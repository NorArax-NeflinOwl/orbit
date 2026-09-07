using Orbit.Core.Abstractions;

namespace Orbit.Core.Folders.DeleteFolder;

[ClientAction(ClientActionCategory.Edit)]
public sealed record DeleteFolderCommand(Guid UserId, Guid FolderId) : IRequest<bool>;
