using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.GetUserById;

/// <summary>
/// Looks up another user's public profile (display name, username, public chat key) by id - used by the
/// chat feature to resolve a conversation partner that's already known (e.g. from the contact list)
/// without going through the identifier-based SearchUserQuery again.
/// </summary>
public sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, User?>
{
    private readonly IUserRepository _userRepository;

    public GetUserByIdQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public Task<User?> HandleAsync(GetUserByIdQuery request, CancellationToken cancellationToken)
        => _userRepository.GetByIdAsync(request.Id, cancellationToken);
}
