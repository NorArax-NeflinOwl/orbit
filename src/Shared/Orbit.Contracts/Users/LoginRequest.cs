namespace Orbit.Contracts.Users;

public sealed record LoginRequest(string EmailOrUserName, string Password);
