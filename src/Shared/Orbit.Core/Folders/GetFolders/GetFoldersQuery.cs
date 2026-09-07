using Orbit.Core.Abstractions;

namespace Orbit.Core.Folders.GetFolders;

/// <summary>The folders this user made. The two built-in ones are not here - see BuiltInFolder.</summary>
public sealed record GetFoldersQuery(Guid UserId) : IRequest<IReadOnlyList<Folder>>;
