namespace Orbit.Contracts.Folders;

/// <summary>
/// One folder somebody made - a tab on the pages made of cards. The three built-in folders (Public,
/// Private and Done) have no row and never arrive here: a client draws them itself, and an item is in
/// one of them exactly when it has no <c>FolderId</c> - see Orbit.Core.Folders.BuiltInFolder.
/// </summary>
/// <param name="Scope">
/// The page it is a tab on - "Notes" or "Tasks", see Orbit.Core.Folders.FolderScope. Every folder the
/// account has arrives whatever the page asking is, and each page draws the scope it is: the dashboard
/// draws both, because it shows both kinds of card.
/// </param>
public sealed record FolderDto(Guid Id, string Name, string Scope, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
