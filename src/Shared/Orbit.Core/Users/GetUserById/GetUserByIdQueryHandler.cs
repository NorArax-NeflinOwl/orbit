using Orbit.Core.Abstractions;
using Orbit.Core.Permissions;

namespace Orbit.Core.Users.GetUserById;

/// <summary>
/// Looks up another user's public profile (display name, username, public chat key) by id - used by the
/// chat feature to resolve a conversation partner that's already known (e.g. from the contact list)
/// without going through the identifier-based SearchUserQuery again.
/// </summary>
public sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, User?>
{
    private readonly IUserRepository _userRepository;
    private readonly UserVisibility _userVisibility;

    public GetUserByIdQueryHandler(IUserRepository userRepository, UserVisibility userVisibility)
    {
        _userRepository = userRepository;
        _userVisibility = userVisibility;
    }

    /// <summary>
    /// Nothing for an account that has not unlocked Contacts: knowing an id is not supposed to be a way
    /// around being invisible - see UserVisibility.
    /// </summary>
    public async Task<User?> HandleAsync(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);
        return user is not null && await _userVisibility.IsFindableAsync(user.Id, cancellationToken) ? user : null;
    }
}
