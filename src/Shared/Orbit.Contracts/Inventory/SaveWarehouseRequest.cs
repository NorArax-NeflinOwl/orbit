namespace Orbit.Contracts.Inventory;

/// <summary>Body for both creating and renaming a warehouse - a warehouse is just a name, so one shape covers both.</summary>
public sealed record SaveWarehouseRequest(string Name);
