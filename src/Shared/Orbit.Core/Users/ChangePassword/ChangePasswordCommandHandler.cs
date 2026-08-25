using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.ChangePassword;

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public ChangePasswordCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    /// <summary>False when the account is gone or the current password doesn't match.</summary>
    public async Task<bool> HandleAsync(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        // An account with no password can't "change" one - it sets its first through SetPasswordCommand,
        // which needs no current password precisely because there is none to prove.
        if (user?.PasswordHash is not { } currentHash || !_passwordHasher.Verify(request.CurrentPassword, currentHash))
        {
            return false;
        }

        user.ChangePassword(_passwordHasher.Hash(request.NewPassword));
        await _userRepository.UpdateAsync(user, cancellationToken);
        return true;
    }
}
