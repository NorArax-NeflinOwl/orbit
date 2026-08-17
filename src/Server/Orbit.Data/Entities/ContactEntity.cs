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
}
