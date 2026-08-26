namespace Orbit.Core.Chat.Groups;

public interface IChatGroupRepository
{
    Task AddAsync(ChatGroup group, CancellationToken cancellationToken);

    /// <summary>
    /// Unscoped on purpose, unlike most repositories here: whether the caller may see this group is a
    /// membership question the group itself answers (ChatGroup.IsMember), and handlers check it right
    /// after loading. Returning null for a non-member instead would make "no such group" and "not yours"
    /// indistinguishable in places that genuinely need to tell them apart.
    /// </summary>
    Task<ChatGroup?> GetByIdAsync(Guid groupId, CancellationToken cancellationToken);

    /// <summary>Every group userId currently belongs to, newest first.</summary>
    Task<IReadOnlyList<ChatGroup>> GetForMemberAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Persists the group's name and its whole membership list as it now stands.</summary>
    Task UpdateAsync(ChatGroup group, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a group and its memberships outright. Only reached when the last member's account is
    /// deleted (see DeleteAccountCommandHandler): a group nobody is in can never be posted to, read, or
    /// joined again, so keeping it would leave a row nothing can reach. Its messages are left alone,
    /// like every other already-delivered message.
    /// </summary>
    Task DeleteAsync(Guid groupId, CancellationToken cancellationToken);
}
