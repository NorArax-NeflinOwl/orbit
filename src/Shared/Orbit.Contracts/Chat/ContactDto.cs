namespace Orbit.Contracts.Chat;

public sealed record ContactDto(Guid UserId, string UserName, string DisplayName, string Email, string? PublicKeyBase64, DateTimeOffset LastMessageAtUtc);
