using Orbit.Core.Abstractions;

namespace Orbit.Core.Folders.CreateFolder;

/// <summary>The scope is the page the tab was made on, and cannot be changed afterwards - see Folder.Scope.</summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record CreateFolderCommand(Guid UserId, string Name, FolderScope Scope) : IRequest<Folder>;
