using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.MoveInventoryToFolder;

/// <summary>
/// Files one inventory under <paramref name="FolderId"/>, or under none when it is null - which puts it
/// back in whichever built-in folder its privacy says (see Orbit.Core.Folders.BuiltInFolder).
///
/// Its own command rather than a field on the save, for the reason Inventory.MoveToFolder gives.
/// Mirrors MoveNoteToFolderCommand.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record MoveInventoryToFolderCommand(Guid UserId, Guid InventoryId, Guid? FolderId) : IRequest<bool>;
