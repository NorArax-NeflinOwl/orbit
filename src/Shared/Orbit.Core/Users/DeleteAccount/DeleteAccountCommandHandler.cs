using Orbit.Core.Abstractions;
using Orbit.Core.Chat.Groups;

namespace Orbit.Core.Users.DeleteAccount;

public sealed class DeleteAccountCommandHandler : IRequestHandler<DeleteAccountCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccountDeletionRepository _accountDeletionRepository;
    private readonly IChatGroupRepository _chatGroupRepository;

    public DeleteAccountCommandHandler(
        IUserRepository userRepository, IPasswordHasher passwordHasher,
        IAccountDeletionRepository accountDeletionRepository, IChatGroupRepository chatGroupRepository)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _accountDeletionRepository = accountDeletionRepository;
        _chatGroupRepository = chatGroupRepository;
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

        await LeaveEveryChatGroupAsync(request.UserId, cancellationToken);
        await _accountDeletionRepository.DeleteAllDataForUserAsync(request.UserId, cancellationToken);
        return true;
    }

    /// <summary>
    /// Takes the account out of its groups through the domain, before the wipe, because leaving is not
    /// just deleting a row: an emptied group has to go, and a group whose only admin this was needs a new
    /// one (see ChatGroup.RemoveDeletedAccount).
    ///
    /// Left behind, a membership is worse than untidy. Group messages are fanned out as one ciphertext
    /// copy per current member and the server accepts a send only if there is exactly one for each
    /// (SendGroupMessageCommandHandler), so a member nobody can encrypt for - their public key went with
    /// their account - makes every later message in that group impossible to send, for everyone in it.
    /// </summary>
    private async Task LeaveEveryChatGroupAsync(Guid userId, CancellationToken cancellationToken)
    {
        foreach (var group in await _chatGroupRepository.GetForMemberAsync(userId, cancellationToken))
        {
            group.RemoveDeletedAccount(userId);
            if (group.IsEmpty)
            {
                await _chatGroupRepository.DeleteAsync(group.Id, cancellationToken);
                continue;
            }

            await _chatGroupRepository.UpdateAsync(group, cancellationToken);
        }
    }
}
