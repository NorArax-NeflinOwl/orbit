using ShareAccessLevel = Orbit.Core.Abstractions.ShareAccessLevel;
using Orbit.Core.Permissions;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Data;
using Orbit.Mobile.Permissions;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Sharing;

/// <summary>
/// Offering several things to one contact in one go - what a list screen's bar does with the things
/// chosen on it (see Orbit.Mobile.Screens.Folders.PickingSeveral). The same two steps
/// <see cref="SharePanel"/> takes for one thing, taken once per thing: the server records each offer,
/// and an encrypted chat message carries each to the recipient - see <see cref="SharedItemSharing"/>.
///
/// Its own class rather than the panel reused, because the panel is about one thing on screen - its
/// link, its request to edit, its name - and none of that means anything for a handful.
/// </summary>
public sealed class SharingSeveral
{
    private readonly ChatRepository _chatRepository;
    private readonly ChatSynchronizer _synchronizer;
    private readonly SharedItemSharing _sharing;
    private readonly UserPermissions _permissions;

    public SharingSeveral(
        ChatRepository chatRepository, ChatSynchronizer synchronizer, SharedItemSharing sharing,
        UserPermissions permissions)
    {
        _chatRepository = chatRepository;
        _synchronizer = synchronizer;
        _sharing = sharing;
        _permissions = permissions;
    }

    /// <summary>Whether this account may share at all - see ApplicationPermission.Sharing.</summary>
    public bool IsAllowed => _permissions.Has(ApplicationPermission.Sharing);

    /// <summary>
    /// Who can be offered anything: the people this account has a conversation with. Asked for afresh
    /// first, for the reason SharePanel gives - best-effort, so offline the cached list is the answer.
    /// </summary>
    public async Task<IReadOnlyList<LocalContact>> ContactsAsync(CancellationToken cancellationToken = default)
    {
        await _synchronizer.SynchroniseContactsAsync(cancellationToken);
        return await _chatRepository.GetContactsAsync(cancellationToken);
    }

    /// <summary>One offer - see <see cref="SharedItemSharing.ShareAsync"/>.</summary>
    public Task<SharingOutcome> ShareAsync(
        SharedItemKind kind, Guid serverId, string name, Guid recipientUserId, ShareAccessLevel accessLevel,
        CancellationToken cancellationToken = default)
        => _sharing.ShareAsync(kind, serverId, name, recipientUserId, accessLevel.ToString(), cancellationToken);
}
