namespace Orbit.Contracts.Users;

public sealed record AuthResponse(string Token, string RefreshToken, Guid UserId, string Email, string DisplayName);
