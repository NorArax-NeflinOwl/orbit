using Orbit.Core.Abstractions;
using Orbit.Core.Chat.Groups;

namespace Orbit.Core.Users.DeleteAccount;

public sealed class DeleteAccountCommandHandler : IRequestHandler<DeleteAccountCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccountDeletionRepository _accountDeletionRepository;
    private readonly IChatGroupRepository _chatGroupRepository;
    private readonly IGoogleIdentityVerifier _googleIdentityVerifier;

    /// <summary>
    /// How recent a Google sign-in has to be to count as the owner confirming it now. A token is good for
    /// an hour, and one kept from the sign-in that opened this session proves only that the session was
    /// once the owner's - which is the thing a stolen session also has.
    /// </summary>
    private static readonly TimeSpan FreshGoogleSignIn = TimeSpan.FromMinutes(10);

    public DeleteAccountCommandHandler(
        IUserRepository userRepository, IPasswordHasher passwordHasher,
        IAccountDeletionRepository accountDeletionRepository, IChatGroupRepository chatGroupRepository,
        IGoogleIdentityVerifier googleIdentityVerifier)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _accountDeletionRepository = accountDeletionRepository;
        _chatGroupRepository = chatGroupRepository;
        _googleIdentityVerifier = googleIdentityVerifier;
    }

    /// <summary>
    /// False when the account is gone, or what was offered to prove it is the owner does not.
    ///
    /// Two ways to prove it. A Google sign-in made a moment ago, for this account's own Google identity -
    /// which also lets a Google-linked account whose password is forgotten delete itself. Or the password,
    /// for an account that has one. An account with neither to offer (Google-only, never given a
    /// password, and sending no token) is still let through for now: installed phone builds send the
    /// empty password and nothing else, and refusing them would leave those accounts no way out inside
    /// the app. Requiring the token of such an account is the last step, once both clients send it - see
    /// info/future-plan.md, "Proving it is you before an account without a password is deleted".
    /// </summary>
    public async Task<bool> HandleAsync(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        // A token that was sent is checked and decides it, whatever else was sent: a client offering one
        // is saying this is how the owner proves themselves, and a token that proves nothing is a refusal.
        if (request.GoogleIdToken is { Length: > 0 } idToken)
        {
            if (!await IsConfirmedByGoogleAsync(user, idToken, cancellationToken))
            {
                return false;
            }
        }
        else if (user.PasswordHash is { } currentHash && !_passwordHasher.Verify(request.Password, currentHash))
        {
            return false;
        }

        await LeaveEveryChatGroupAsync(request.UserId, cancellationToken);
        await _accountDeletionRepository.DeleteAllDataForUserAsync(request.UserId, cancellationToken);
        return true;
    }

    /// <summary>
    /// Whether the token is a genuine Google sign-in, for the Google identity this account is linked to,
    /// made within <see cref="FreshGoogleSignIn"/>. An account linked to no Google identity cannot be
    /// confirmed this way at all.
    /// </summary>
    private async Task<bool> IsConfirmedByGoogleAsync(User user, string idToken, CancellationToken cancellationToken)
    {
        if (user.GoogleSubjectId is not { } linkedSubjectId)
        {
            return false;
        }

        var identity = await _googleIdentityVerifier.VerifyAsync(idToken, cancellationToken);
        return identity is not null
            && identity.SubjectId == linkedSubjectId
            && DateTimeOffset.UtcNow - identity.IssuedAtUtc <= FreshGoogleSignIn;
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
