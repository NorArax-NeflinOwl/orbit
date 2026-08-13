namespace Orbit.Contracts.Users;

public sealed record AuthResponse(string Token, Guid UserId, string Email, string DisplayName);
