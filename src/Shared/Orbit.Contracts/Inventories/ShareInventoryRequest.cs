namespace Orbit.Contracts.Inventories;

/// <summary>AccessLevel is "ReadOnly", "Share", or "CanEdit" (see Orbit.Core.Abstractions.ShareAccessLevel).</summary>
public sealed record ShareInventoryRequest(Guid RecipientUserId, string AccessLevel = "ReadOnly");
