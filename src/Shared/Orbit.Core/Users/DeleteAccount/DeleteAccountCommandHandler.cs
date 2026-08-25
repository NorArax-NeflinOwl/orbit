using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.DeleteAccount;

public sealed class DeleteAccountCommandHandler : IRequestHandler<DeleteAccountCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccountDeletionRepository _accountDeletionRepository;

    public DeleteAccountCommandHandler(
        IUserRepository userRepository, IPasswordHasher passwordHasher, IAccountDeletionRepository accountDeletionRepository)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _accountDeletionRepository = accountDeletionRepository;
    }

    /// <summary>False when the account is gone or the password doesn't match - same signal as ChangePasswordCommandHandler.</summary>
    public async Task<bool> HandleAsync(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return false;
        }

        await _accountDeletionRepository.DeleteAllDataForUserAsync(request.UserId, cancellationToken);
        return true;
    }
}
