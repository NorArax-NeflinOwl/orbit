namespace Orbit.Contracts.Users;

public sealed record RegisterUserRequest(string Email, string DisplayName, string Password);
