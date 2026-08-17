using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SearchUser;

public sealed class SearchUserQueryHandler : IRequestHandler<SearchUserQuery, User?>
{
    private readonly IUserRepository _userRepository;

    public SearchUserQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
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

        return user is null || user.Id == request.RequestingUserId ? null : user;
    }
}
