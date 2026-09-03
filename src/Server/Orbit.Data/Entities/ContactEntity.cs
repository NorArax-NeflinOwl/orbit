namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a chat contact relationship, mapped separately from
/// <see cref="Orbit.Core.Chat.Contact"/> so schema changes don't force changes onto domain logic, and
/// vice versa.
/// </summary>
public sealed class ContactEntity
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid ContactUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset LastMessageAtUtc { get; set; }

    /// <summary>Put away by the owner of this row - see Orbit.Core.Chat.Contact.IsArchived.</summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Where this owner's view of the conversation begins, if they have ever cleared it - see
    /// Orbit.Core.Chat.Contact.HistoryClearedAtUtc. Null means from the beginning.
    /// </summary>
    public DateTimeOffset? HistoryClearedAtUtc { get; set; }
}
