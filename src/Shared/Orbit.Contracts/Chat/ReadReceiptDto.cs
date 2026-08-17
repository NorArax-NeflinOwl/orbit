namespace Orbit.Contracts.Chat;

/// <summary>Null ReadUpToUtc means none of the caller's messages to the other party have been read yet.</summary>
public sealed record ReadReceiptDto(DateTimeOffset? ReadUpToUtc);
