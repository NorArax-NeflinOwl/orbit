using Orbit.Core.Users;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IUserRepository"/> stub for unit tests that need real add/lookup behavior
/// without spinning up SQLite.
/// </summary>
internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<User> _users = [];

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_users.FirstOrDefault(user => user.Id == id));

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        => Task.FromResult(_users.FirstOrDefault(user => user.Email == email));

    public Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken)
        => Task.FromResult(_users.FirstOrDefault(user => user.UserName == userName));

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task<User?> GetByGoogleSubjectIdAsync(string googleSubjectId, CancellationToken cancellationToken)
        => Task.FromResult(_users.FirstOrDefault(user => user.GoogleSubjectId == googleSubjectId));

    public Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        // Handlers mutate the same User instance this repository already holds a reference to, so
        // there is nothing to replace here - this mirrors InMemoryNoteRepository.UpdateAsync.
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<User>>(_users.Where(user => ids.Contains(user.Id)).ToList());
}