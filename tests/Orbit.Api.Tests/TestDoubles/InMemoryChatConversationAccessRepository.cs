using Orbit.Core.Chat;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IChatConversationAccessRepository"/> stub for unit tests that need real
/// create/lookup/approve behavior, including direction-agnostic pair matching, without spinning up
/// SQLite.
/// </summary>
internal sealed class InMemoryChatConversationAccessRepository : IChatConversationAccessRepository
{
    private readonly List<ChatConversationAccess> _accesses = [];

    public Task<ChatConversationAccess?> GetAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken)
        => Task.FromResult(Find(userId, otherUserId));

    public Task EnsureCreatedAsync(Guid initiatedByUserId, Guid otherUserId, CancellationToken cancellationToken)
    {
        if (Find(initiatedByUserId, otherUserId) is null)
        {
            _accesses.Add(ChatConversationAccess.Create(initiatedByUserId, otherUserId));
        }

        return Task.CompletedTask;
    }

    public Task<bool> ApproveAsync(Guid approvingUserId, Guid otherUserId, CancellationToken cancellationToken)
    {
        var access = Find(approvingUserId, otherUserId);
        if (access is null || access.InitiatedByUserId == approvingUserId)
        {
            return Task.FromResult(false);
        }

        access.Approve();
        return Task.FromResult(true);
    }

    private ChatConversationAccess? Find(Guid userId, Guid otherUserId)
        => _accesses.FirstOrDefault(access =>
            (access.InitiatedByUserId == userId && access.OtherUserId == otherUserId) ||
            (access.InitiatedByUserId == otherUserId && access.OtherUserId == userId));
    public Task<IReadOnlyDictionary<Guid, ChatConversationAccess>> GetAllForUserAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, ChatConversationAccess> byOtherParty = _accesses
            .Where(access => access.InitiatedByUserId == userId || access.OtherUserId == userId)
            .ToDictionary(
                access => access.InitiatedByUserId == userId ? access.OtherUserId : access.InitiatedByUserId,
                access => access);

        return Task.FromResult(byOtherParty);
    }
}