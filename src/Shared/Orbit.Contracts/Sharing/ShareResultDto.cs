namespace Orbit.Contracts.Sharing;

/// <summary>
/// Response body for every "share this" endpoint (`POST /api/notes/{id}/shares` and its task-list and
/// calendar-event equivalents). AlreadyShared distinguishes a freshly created offer from a duplicate
/// request the server turned into a reminder of an existing one - see
/// Orbit.Core.Abstractions.ShareOutcome, which this mirrors across the HTTP boundary. Either way,
/// ShareId is the id the client encrypts into a chat message to notify the recipient.
/// </summary>
public sealed record ShareResultDto(Guid ShareId, bool AlreadyShared);
