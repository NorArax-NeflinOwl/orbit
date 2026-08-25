using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.LinkGoogleAccount;

public sealed class LinkGoogleAccountCommandHandler : IRequestHandler<LinkGoogleAccountCommand, LinkGoogleAccountResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IGoogleIdentityVerifier _googleIdentityVerifier;

    public LinkGoogleAccountCommandHandler(IUserRepository userRepository, IGoogleIdentityVerifier googleIdentityVerifier)
    {
        _userRepository = userRepository;
        _googleIdentityVerifier = googleIdentityVerifier;
    }

    public async Task<LinkGoogleAccountResult> HandleAsync(LinkGoogleAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return LinkGoogleAccountResult.UserNotFound;
        }

        var identity = await _googleIdentityVerifier.VerifyAsync(request.IdToken, cancellationToken);
        if (identity is null)
        {
            return LinkGoogleAccountResult.InvalidToken;
        }

        // One Google identity can only ever point at one Orbit account, or signing in with Google would
        // be ambiguous.
        var existing = await _userRepository.GetByGoogleSubjectIdAsync(identity.SubjectId, cancellationToken);
        if (existing is not null && existing.Id != user.Id)
        {
            return LinkGoogleAccountResult.AlreadyLinkedElsewhere;
        }

        user.LinkGoogle(identity.SubjectId);
        await _userRepository.UpdateAsync(user, cancellationToken);
        return LinkGoogleAccountResult.Success;
    }
}

public sealed class UnlinkGoogleAccountCommandHandler : IRequestHandler<UnlinkGoogleAccountCommand, LinkGoogleAccountResult>
{
    private readonly IUserRepository _userRepository;

    public UnlinkGoogleAccountCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<LinkGoogleAccountResult> HandleAsync(UnlinkGoogleAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return LinkGoogleAccountResult.UserNotFound;
        }

        // Google is the only way into an account that never set a password - unlinking it would lock the
        // owner out permanently, so it's refused until they set one.
        if (!user.HasPassword)
        {
            return LinkGoogleAccountResult.WouldLockAccountOut;
        }

        user.UnlinkGoogle();
        await _userRepository.UpdateAsync(user, cancellationToken);
        return LinkGoogleAccountResult.Success;
    }
}
