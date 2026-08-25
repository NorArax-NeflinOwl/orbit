using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SignInWithGoogle;

public sealed class SignInWithGoogleCommandHandler : IRequestHandler<SignInWithGoogleCommand, User?>
{
    private readonly IUserRepository _userRepository;
    private readonly IGoogleIdentityVerifier _googleIdentityVerifier;

    public SignInWithGoogleCommandHandler(IUserRepository userRepository, IGoogleIdentityVerifier googleIdentityVerifier)
    {
        _userRepository = userRepository;
        _googleIdentityVerifier = googleIdentityVerifier;
    }

    /// <summary>
    /// One entry point for both "log in" and "register", because from the browser's side they are the
    /// same gesture. Resolution order matters:
    /// <list type="number">
    /// <item>An account already linked to this Google subject - the normal returning case.</item>
    /// <item>An account with the same address, which gets linked. Safe because Google only issues a token
    /// for an address it verified, so whoever holds this token demonstrably controls that mailbox - the
    /// same proof Orbit's own email verification asks for.</item>
    /// <item>Otherwise a brand-new account, with a login derived from the address.</item>
    /// </list>
    /// </summary>
    public async Task<User?> HandleAsync(SignInWithGoogleCommand request, CancellationToken cancellationToken)
    {
        var identity = await _googleIdentityVerifier.VerifyAsync(request.IdToken, cancellationToken);
        if (identity is null)
        {
            return null;
        }

        var linked = await _userRepository.GetByGoogleSubjectIdAsync(identity.SubjectId, cancellationToken);
        if (linked is not null)
        {
            return linked;
        }

        var byEmail = await _userRepository.GetByEmailAsync(identity.Email, cancellationToken);
        if (byEmail is not null)
        {
            byEmail.LinkGoogle(identity.SubjectId);
            await _userRepository.UpdateAsync(byEmail, cancellationToken);
            return byEmail;
        }

        var user = User.CreateFromGoogle(
            identity.Email, await GenerateAvailableUserNameAsync(identity.Email, cancellationToken),
            identity.DisplayName, identity.SubjectId);
        await _userRepository.AddAsync(user, cancellationToken);
        return user;
    }

    /// <summary>
    /// Derives a login from the address's local part, adding a numeric suffix until it's free - the user
    /// never chose one, so something has to, and they can change it in Options afterwards.
    /// </summary>
    private async Task<string> GenerateAvailableUserNameAsync(string email, CancellationToken cancellationToken)
    {
        var baseName = new string(email.Split('@')[0].Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (baseName.Length == 0)
        {
            baseName = "user";
        }

        if (await _userRepository.GetByUserNameAsync(baseName, cancellationToken) is null)
        {
            return baseName;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{baseName}{suffix}";
            if (await _userRepository.GetByUserNameAsync(candidate, cancellationToken) is null)
            {
                return candidate;
            }
        }
    }
}
