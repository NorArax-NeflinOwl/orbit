using Orbit.Contracts;

namespace Orbit.Contracts.Inventory;

/// <summary>
/// Body for creating a warehouse (name only - Items is empty) and for saving one (name plus its whole
/// intended item list, since items missing from Items are deleted).
///
/// IsPrivate marks a warehouse only its owner can read: Name and Items then travel empty and the real
/// values are sealed inside EncryptedContent, which the browser fills in and the server never opens.
/// </summary>
public sealed record SaveWarehouseRequest(
    string Name, IReadOnlyList<WarehouseItemDto> Items, bool IsPrivate = false, EncryptedContentDto? EncryptedContent = null);
