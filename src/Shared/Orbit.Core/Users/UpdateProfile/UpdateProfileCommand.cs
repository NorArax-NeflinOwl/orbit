using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.UpdateProfile;

/// <summary>
/// The parts of a profile that change without proving anything by email. The address deliberately isn't
/// here - it only ever changes through a confirmed code (see VerificationCodePurpose.EmailVerification).
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record UpdateProfileCommand(Guid UserId, string DisplayName, string UserName) : IRequest<UpdateProfileResult>;

public enum UpdateProfileResult
{
    Success,
    UserNameTaken,
    UserNotFound
}
