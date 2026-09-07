namespace Orbit.Contracts.Folders;

/// <summary>
/// One folder somebody made - a tab on the pages made of cards. The three built-in folders (Public,
/// Private and Done) have no row and never arrive here: a client draws them itself, and an item is in
/// one of them exactly when it has no <c>FolderId</c> - see Orbit.Core.Folders.BuiltInFolder.
/// </summary>
public sealed record FolderDto(Guid Id, string Name, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
