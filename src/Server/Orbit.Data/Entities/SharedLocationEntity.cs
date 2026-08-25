namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of one shared position - see <see cref="Orbit.Core.Location.SharedLocation"/> for
/// why there is exactly one row per (sharer, recipient) pair and why it is overwritten rather than
/// appended to. The ciphertext is opaque to the server, the same way a chat message's is.
/// </summary>
public sealed class SharedLocationEntity
{
    public Guid Id { get; set; }
    public Guid SharerUserId { get; set; }
    public Guid RecipientUserId { get; set; }
    public string CiphertextBase64 { get; set; } = string.Empty;
    public string NonceBase64 { get; set; } = string.Empty;
    public bool IsContinuous { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
