using Orbit.Core.Abstractions;
using Orbit.Core.Permissions;

namespace Orbit.Core.Users.SearchUser;

public sealed class SearchUserQueryHandler : IRequestHandler<SearchUserQuery, User?>
{
    private readonly IUserRepository _userRepository;
    private readonly UserVisibility _userVisibility;

    public SearchUserQueryHandler(IUserRepository userRepository, UserVisibility userVisibility)
    {
        _userRepository = userRepository;
        _userVisibility = userVisibility;
    }

    /// <summary>
    /// Exact match only, tried as an email address and then as a username - Contacts search
    /// intentionally does not do partial/fuzzy matching, so it can't be used to enumerate the user base
    /// by trying prefixes. Never returns the requesting user themselves, since starting a chat with
    /// yourself isn't a supported scenario.
    /// </summary>
    public async Task<User?> HandleAsync(SearchUserQuery request, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(identifier, cancellationToken)
            ?? await _userRepository.GetByUserNameAsync(identifier, cancellationToken);

        if (user is null || user.Id == request.RequestingUserId)
        {
            return null;
        }

        // An account that has not unlocked Contacts is not findable - the same "no such user" a made-up
        // address gets, because saying "found, but hidden" would be finding them.
        return await _userVisibility.IsFindableAsync(user.Id, cancellationToken) ? user : null;
    }
}
