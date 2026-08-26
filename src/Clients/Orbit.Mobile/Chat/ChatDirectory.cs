using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;

namespace Orbit.Mobile.Chat;

/// <summary>
/// Who can be written to and what each group's membership is, <b>as the server has them right now</b>.
/// </summary>
public sealed class ChatDirectory
{
    private readonly IReadOnlyDictionary<Guid, string> _publicKeysByUserId;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> _otherMembersByGroupId;

    internal ChatDirectory(
        IReadOnlyDictionary<Guid, string> publicKeysByUserId,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> otherMembersByGroupId)
    {
        _publicKeysByUserId = publicKeysByUserId;
        _otherMembersByGroupId = otherMembersByGroupId;
    }

    /// <summary>Null when they have published no key, so nothing can be encrypted for them.</summary>
    public string? FindPublicKey(Guid userId) => _publicKeysByUserId.GetValueOrDefault(userId);

    /// <summary>
    /// Everyone in the group but the signed-in user - exactly the set a fan-out has to cover. Null when
    /// the group is gone, or this account is no longer in it: the server answers the same way to either.
    /// </summary>
    public IReadOnlyList<Guid>? FindOtherMembers(Guid groupId) => _otherMembersByGroupId.GetValueOrDefault(groupId);
}

/// <summary>
/// Fetches the directory. Read fresh every time and never cached between pieces of work, which is the
/// whole reason it exists as a step of its own: a key the recipient has replaced would seal a message
/// nobody can open, and a membership list that has moved on would have the fan-out refused.
///
/// Sending and editing both need it, and for the same reason - editing a group message is the same
/// fan-out as sending one, because leaving a copy behind would show different members different words.
/// </summary>
public sealed class ChatDirectoryReader
{
    private readonly ChatClient _chatClient;
    private readonly UsersClient _usersClient;
    private readonly SessionStore _sessionStore;

    public ChatDirectoryReader(ChatClient chatClient, UsersClient usersClient, SessionStore sessionStore)
    {
        _chatClient = chatClient;
        _usersClient = usersClient;
        _sessionStore = sessionStore;
    }

    /// <param name="includeContacts">
    /// Whether anything here is addressed to a person rather than a group. Skipped when nothing is, so a
    /// group-only flush does not fetch a contact list it will not read.
    /// </param>
    /// <param name="groupIds">The groups whose membership and members' keys are needed - usually one.</param>
    public async Task<ChatDirectory> ReadAsync(
        bool includeContacts, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken = default)
    {
        var publicKeys = new Dictionary<Guid, string>();
        var otherMembers = new Dictionary<Guid, IReadOnlyList<Guid>>();

        if (includeContacts)
        {
            foreach (var contact in await _chatClient.GetContactsAsync(cancellationToken))
            {
                if (contact.PublicKeyBase64 is { } publicKey)
                {
                    publicKeys[contact.UserId] = publicKey;
                }
            }
        }

        if (groupIds.Count == 0)
        {
            return new ChatDirectory(publicKeys, otherMembers);
        }

        var ownUserId = await RequireSignedInUserIdAsync();
        foreach (var group in await _chatClient.GetGroupsAsync(cancellationToken))
        {
            if (groupIds.Contains(group.Id))
            {
                otherMembers[group.Id] = group.Members
                    .Select(member => member.UserId)
                    .Where(userId => userId != ownUserId)
                    .ToList();
            }
        }

        // A group can hold people the sender has never had a conversation with, so the contact list does
        // not cover them and each has to be looked up by id.
        foreach (var userId in otherMembers.Values.SelectMany(members => members).Distinct())
        {
            if (publicKeys.ContainsKey(userId))
            {
                continue;
            }

            if (await _usersClient.FindAsync(userId, cancellationToken) is { PublicKeyBase64: { } publicKey })
            {
                publicKeys[userId] = publicKey;
            }
        }

        return new ChatDirectory(publicKeys, otherMembers);
    }

    private async Task<Guid> RequireSignedInUserIdAsync()
        => await _sessionStore.GetAsync() is { } session
            ? session.UserId
            : throw new EncryptionKeyLockedException();
}
