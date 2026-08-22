namespace Orbit.Contracts.Notes;

/// <summary>AccessLevel is "ReadOnly" or "CanEdit" (see Orbit.Core.Abstractions.ShareAccessLevel).</summary>
public sealed record ShareNoteRequest(Guid RecipientUserId, string AccessLevel = "ReadOnly");
