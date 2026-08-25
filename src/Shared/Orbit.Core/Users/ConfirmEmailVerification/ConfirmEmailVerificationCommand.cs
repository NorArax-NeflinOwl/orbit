using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.ConfirmEmailVerification;

/// <summary>False when the code is wrong, expired, already used, or out of attempts - the same answer either way, so a wrong guess reveals nothing.</summary>
public sealed record ConfirmEmailVerificationCommand(Guid UserId, string Code) : IRequest<bool>;
