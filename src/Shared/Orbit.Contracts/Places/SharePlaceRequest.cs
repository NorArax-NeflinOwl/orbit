namespace Orbit.Contracts.Places;

/// <summary>Who to hand a place to, and how much of it they get. Mirrors ShareNoteRequest.</summary>
public sealed record SharePlaceRequest(Guid RecipientUserId, string AccessLevel = "ReadOnly");
