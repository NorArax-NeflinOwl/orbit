using Orbit.Core.Abstractions;

namespace Orbit.Core.Location.ShareLocation;

/// <summary>
/// Shares the caller's position with one recipient, or replaces the point already shared with them.
/// The ciphertext arrives sealed for that recipient specifically - see SharedLocation.
/// </summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record ShareLocationCommand(
    Guid SharerUserId, Guid RecipientUserId, string CiphertextBase64, string NonceBase64, bool IsContinuous) : IRequest<bool>;
