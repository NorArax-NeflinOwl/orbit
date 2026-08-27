using Orbit.Core.Abstractions;
using Orbit.Core.Permissions;

namespace Orbit.Core.Users.GetUsersByIds;

/// <summary>
/// Caps how many ids one request may ask about. Not a security boundary - the same profiles are
/// readable one at a time - but an unbounded IN list is a query nobody planned for, and a caller that
/// needs more than this is doing something the roster shape does not describe.
/// </summary>
public sealed class GetUsersByIdsQueryHandler : IRequestHandler<GetUsersByIdsQuery, IReadOnlyList<User>>
{
    public const int MaxIds = 200;

    private readonly IUserRepository _userRepository;

    public GetUsersByIdsQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<User>> HandleAsync(GetUsersByIdsQuery request, CancellationToken cancellationToken)
    {
        // Duplicates collapse rather than being refused: a caller assembling ids from several places
        // asked about the same person twice, which is not a mistake worth an error.
        var ids = request.Ids.Distinct().ToList();
        if (ids.Count > MaxIds)
        {
            throw new InvalidRequestException($"Ask about at most {MaxIds} people at a time.");
        }

        // Deliberately not filtered by UserVisibility: this resolves names for people the caller already
        // has a conversation or a group with, and blanking those would turn an existing chat into a row
        // of "Someone" without hiding anybody from anybody. Being invisible is about not being *found* -
        // see SearchUserQueryHandler and GetUserByIdQueryHandler, which do filter.
        return await _userRepository.GetByIdsAsync(ids, cancellationToken);
    }
}
