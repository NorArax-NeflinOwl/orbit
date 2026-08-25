namespace Orbit.Contracts.Users;

/// <summary>
/// One position shared between two people, still encrypted. Only the two whose keys made the ciphertext
/// can read it - see Orbit.Core.Location.SharedLocation. IsContinuous tells the recipient whether to
/// expect this to keep changing; UpdatedAtUtc is when the point currently stored was recorded.
/// </summary>
public sealed record SharedLocationDto(
    Guid SharerUserId, Guid RecipientUserId, string CiphertextBase64, string NonceBase64,
    bool IsContinuous, DateTimeOffset UpdatedAtUtc);

/// <summary>
/// Shares the caller's position with one recipient, or replaces what is already shared with them. The
/// browser seals the point for that recipient before sending; the server stores what it cannot read.
/// </summary>
public sealed record ShareLocationRequest(
    Guid RecipientUserId, string CiphertextBase64, string NonceBase64, bool IsContinuous);
