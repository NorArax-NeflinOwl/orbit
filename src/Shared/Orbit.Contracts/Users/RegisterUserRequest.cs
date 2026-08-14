namespace Orbit.Contracts.Users;

public sealed record RegisterUserRequest(string Email, string UserName, string DisplayName, string Password);
