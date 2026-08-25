namespace Orbit.Contracts.Inventory;

/// <summary>AccessLevel is "ReadOnly", "Share", or "CanEdit" (see Orbit.Core.Abstractions.ShareAccessLevel).</summary>
public sealed record ShareWarehouseRequest(Guid RecipientUserId, string AccessLevel = "ReadOnly");
