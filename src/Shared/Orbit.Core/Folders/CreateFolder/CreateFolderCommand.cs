using Orbit.Core.Abstractions;

namespace Orbit.Core.Folders.CreateFolder;

[ClientAction(ClientActionCategory.Save)]
public sealed record CreateFolderCommand(Guid UserId, string Name) : IRequest<Folder>;
