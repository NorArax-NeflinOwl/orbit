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

    /// <summary>
    /// False when the account is gone, or it has a password and the one given doesn't match. An account
    /// with no password (Google-only, see SetPasswordCommand) needs none here either - being signed in
    /// is the proof, same reasoning as SetPasswordCommand.
    /// </summary>
    public async Task<bool> HandleAsync(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        if (user.PasswordHash is { } currentHash && !_passwordHasher.Verify(request.Password, currentHash))
        {
            return false;
        }

        await _accountDeletionRepository.DeleteAllDataForUserAsync(request.UserId, cancellationToken);
        return true;
    }
}
