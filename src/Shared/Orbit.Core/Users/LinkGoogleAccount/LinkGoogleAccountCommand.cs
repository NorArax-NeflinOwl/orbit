using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.LinkGoogleAccount;

/// <summary>Connects the signed-in account to a Google identity, so it can also be signed into with Google.</summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record LinkGoogleAccountCommand(Guid UserId, string IdToken) : IRequest<LinkGoogleAccountResult>;

/// <summary>Disconnects it again. Refused when it would leave the account with no way to sign in at all.</summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record UnlinkGoogleAccountCommand(Guid UserId) : IRequest<LinkGoogleAccountResult>;

public enum LinkGoogleAccountResult
{
    Success,

    /// <summary>The Google token didn't check out.</summary>
    InvalidToken,

    /// <summary>That Google account is already linked to a different Orbit account.</summary>
    AlreadyLinkedElsewhere,

    /// <summary>Unlinking would leave an account with neither a password nor Google - i.e. no way back in.</summary>
    WouldLockAccountOut,

    UserNotFound
}
