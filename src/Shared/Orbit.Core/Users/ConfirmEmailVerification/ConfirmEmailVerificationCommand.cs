using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.ConfirmEmailVerification;

public sealed record ConfirmEmailVerificationCommand(Guid UserId, string Code) : IRequest<EmailVerificationConfirmResult>;

public enum EmailVerificationConfirmResult
{
    Confirmed,

    /// <summary>
    /// Wrong, expired, already used, or out of attempts - the same answer either way, so a wrong guess
    /// reveals nothing about which it was.
    /// </summary>
    InvalidCode,

    /// <summary>
    /// The address was free when the code was issued and belongs to somebody else now. The gap between
    /// the two is however long the code lives, which is long enough for another account to claim the
    /// address in between - so being free at the start is not the same as being free at the finish.
    /// </summary>
    EmailTaken
}
