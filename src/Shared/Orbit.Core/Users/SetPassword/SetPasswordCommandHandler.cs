using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SetPassword;

public sealed class SetPasswordCommandHandler : IRequestHandler<SetPasswordCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public SetPasswordCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    /// <summary>False when the account is gone, or already has a password - see the command's comment.</summary>
    public async Task<bool> HandleAsync(SetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null || user.HasPassword)
        {
            return false;
        }

        user.ChangePassword(_passwordHasher.Hash(request.NewPassword));
        await _userRepository.UpdateAsync(user, cancellationToken);
        return true;
    }
}
