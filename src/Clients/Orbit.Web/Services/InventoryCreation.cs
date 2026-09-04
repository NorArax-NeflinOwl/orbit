namespace Orbit.Web.Services;

/// <summary>
/// What asking the server for a new inventory came to: the new inventory's id, or the sentence it gave
/// for refusing. The saving half of this pair has always answered with an <see cref="EditOutcome"/>;
/// creating answered with an id and threw the refusal away, which is how a server that had said exactly
/// what was wrong became "Failed to save the inventory. Try again." on screen.
/// </summary>
public readonly record struct InventoryCreation(Guid? Id, string? RefusedBecause)
{
    public static InventoryCreation Created(Guid id) => new(id, null);

    public static InventoryCreation Refused(string reason) => new(null, reason);

    public bool WasCreated => Id is not null;
}
