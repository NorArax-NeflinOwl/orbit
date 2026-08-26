using Orbit.Core.Abstractions;

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

        return await _userRepository.GetByIdsAsync(ids, cancellationToken);
    }
}
