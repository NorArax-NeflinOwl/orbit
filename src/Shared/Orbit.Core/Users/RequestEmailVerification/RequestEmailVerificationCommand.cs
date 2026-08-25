using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.RequestEmailVerification;

/// <summary>
/// Emails a verification code to EmailAddress. Passing the account's current address re-verifies it;
/// passing a different one starts an email *change*, which only completes once the code is confirmed -
/// see VerificationCodePurpose.EmailVerification.
/// </summary>
public sealed record RequestEmailVerificationCommand(Guid UserId, string EmailAddress) : IRequest<EmailVerificationRequestResult>;

public enum EmailVerificationRequestResult
{
    Sent,

    /// <summary>Another account already uses that address, so switching to it would collide.</summary>
    EmailTaken,

    UserNotFound
}
