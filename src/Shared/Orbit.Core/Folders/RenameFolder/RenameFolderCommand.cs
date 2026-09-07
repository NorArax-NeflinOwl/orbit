using Orbit.Core.Abstractions;

namespace Orbit.Core.Folders.RenameFolder;

[ClientAction(ClientActionCategory.Edit)]
public sealed record RenameFolderCommand(Guid UserId, Guid FolderId, string Name) : IRequest<bool>;
