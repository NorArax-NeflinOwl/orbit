namespace Orbit.Contracts.Inventory;

/// <summary>
/// A warehouse as one caller sees it. IsShared/SharedByUserName/AccessLevel describe that caller's own
/// relationship to it rather than anything stored on the row - see Orbit.Core.Inventory.Warehouse.
/// LockedByUserName names whoever currently holds the edit lock, and is null when nobody does (or when
/// it's the caller's own lock, which never blocks them). Mirrors NoteDto.
/// </summary>
public sealed record WarehouseDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool IsShared,
    string? SharedByUserName,
    string AccessLevel,
    string? LockedByUserName);
