namespace Orbit.Contracts.Users;

public sealed record LoginRequest(string EmailOrUserName, string Password);

/// <summary>
/// Why a sign-in was refused. <paramref name="Reason"/> is one of Orbit.Core.Users.Login.LoginRejection's
/// names and is what a client branches on; <paramref name="Message"/> is the same thing said in words,
/// for anything that only shows a response body. Mirrors RegistrationConflictDto.
/// </summary>
public sealed record LoginRejectionDto(string Reason, string Message);
