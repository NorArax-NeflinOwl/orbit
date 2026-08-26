namespace Orbit.Contracts.Users;

public sealed record RegisterUserRequest(string Email, string UserName, string DisplayName, string Password);

/// <summary>
/// Why a registration was refused. <paramref name="Reason"/> is "EmailTaken" or "UserNameTaken" and is
/// what a client branches on; <paramref name="Message"/> is the same thing said in words, for anything
/// that only shows a response body.
/// </summary>
public sealed record RegistrationConflictDto(string Reason, string Message);
