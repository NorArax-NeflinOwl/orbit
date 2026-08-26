using Orbit.Core.Chat.Groups;

namespace Orbit.Api.Tests.TestDoubles;

internal sealed class InMemoryChatGroupRepository : IChatGroupRepository
{
    private readonly List<ChatGroup> _groups = [];

    public Task AddAsync(ChatGroup group, CancellationToken cancellationToken)
    {
        _groups.Add(group);
        return Task.CompletedTask;
    }

    public Task<ChatGroup?> GetByIdAsync(Guid groupId, CancellationToken cancellationToken)
        => Task.FromResult(_groups.FirstOrDefault(group => group.Id == groupId));

    public Task<IReadOnlyList<ChatGroup>> GetForMemberAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ChatGroup>>(_groups.Where(group => group.IsMember(userId)).ToList());

    /// <summary>
    /// Handlers mutate the same ChatGroup instance this repository already holds, so there is nothing to
    /// replace - mirroring InMemoryNoteRepository and the EF repository's already-tracked entity.
    /// </summary>
    public Task UpdateAsync(ChatGroup group, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DeleteAsync(Guid groupId, CancellationToken cancellationToken)
    {
        _groups.RemoveAll(group => group.Id == groupId);
        return Task.CompletedTask;
    }

    /// <summary>Every group still stored, for tests asserting what survived an account deletion.</summary>
    public IReadOnlyList<ChatGroup> Groups => _groups;
}
