using Orbit.Core.Abstractions;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Chat.Groups.ShareGroupHistory;

/// <summary>
/// Stores the re-encrypted history and says so in the conversation. Returns how many copies were
/// actually written, which is not always how many were offered: a message the sharer cannot prove they
/// hold is dropped, and one the recipient already has is skipped rather than duplicated.
/// </summary>
public sealed class ShareGroupHistoryCommandHandler : IRequestHandler<ShareGroupHistoryCommand, int>
{
    private readonly IChatGroupRepository _chatGroupRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IChatGroupAnnouncementRepository _chatGroupAnnouncementRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public ShareGroupHistoryCommandHandler(
        IChatGroupRepository chatGroupRepository, IChatMessageRepository chatMessageRepository,
        IChatGroupAnnouncementRepository chatGroupAnnouncementRepository,
        ILiveUpdatePublisher liveUpdatePublisher)
    {
        _chatGroupRepository = chatGroupRepository;
        _chatMessageRepository = chatMessageRepository;
        _chatGroupAnnouncementRepository = chatGroupAnnouncementRepository;
        _liveUpdatePublisher = liveUpdatePublisher;
    }

    public async Task<int> HandleAsync(ShareGroupHistoryCommand request, CancellationToken cancellationToken)
    {
        var group = await _chatGroupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group is null || !group.IsMember(request.ActorUserId))
        {
            return 0;
        }

        group.EnsureHistoryCanBeSharedWith(request.ActorUserId, request.RecipientUserId);

        var originals = await ReadableBySharerAsync(request, cancellationToken);
        var alreadyHeld = await AlreadyHeldByRecipientAsync(request, cancellationToken);

        var written = 0;
        foreach (var copy in request.Copies)
        {
            // A message the sharer holds no readable copy of is one they were never able to open, so
            // ciphertext offered in its name is content they invented. Dropped rather than refused: the
            // browser sends what it found, and a conversation it read halfway through a deletion should
            // still hand over the rest.
            if (!originals.TryGetValue(copy.GroupMessageId, out var original) || !alreadyHeld.Add(copy.GroupMessageId))
            {
                continue;
            }

            await _chatMessageRepository.AddAsync(
                ChatMessage.CreateSharedHistoryCopy(original, request.RecipientUserId, copy.CiphertextBase64, copy.NonceBase64),
                cancellationToken);
            written++;
        }

        await AnnounceHistorySharedAsync(request, cancellationToken);

        // The recipient above all - a conversation that was empty a moment ago now has its history in
        // it - and the rest of the group, whose thread has gained a line saying it was shared.
        await _liveUpdatePublisher.ChatChangedAsync(
            [.. group.Members.Select(member => member.UserId).Append(request.RecipientUserId).Distinct()],
            cancellationToken);
        return written;
    }

    /// <summary>
    /// The sharer's own view of the group, keyed by posting rather than by row: what they may pass on is
    /// exactly what they can read, which is the same set the conversation screen shows them.
    /// </summary>
    private async Task<Dictionary<Guid, ChatMessage>> ReadableBySharerAsync(
        ShareGroupHistoryCommand request, CancellationToken cancellationToken)
    {
        var readable = await _chatMessageRepository.GetGroupConversationAsync(
            request.GroupId, request.ActorUserId, sinceUtc: null, cancellationToken);

        return readable
            .Where(message => message.GroupMessageId is not null)
            .GroupBy(message => message.GroupMessageId!.Value)
            .ToDictionary(copies => copies.Key, copies => copies.First());
    }

    /// <summary>
    /// What the recipient can already open, so a share repeated after a failed one - or run twice by a
    /// double-clicked button - adds nothing a second time.
    /// </summary>
    private async Task<HashSet<Guid>> AlreadyHeldByRecipientAsync(
        ShareGroupHistoryCommand request, CancellationToken cancellationToken)
    {
        var held = await _chatMessageRepository.GetGroupConversationAsync(
            request.GroupId, request.RecipientUserId, sinceUtc: null, cancellationToken);

        return held
            .Where(message => message.GroupMessageId is not null)
            .Select(message => message.GroupMessageId!.Value)
            .ToHashSet();
    }

    /// <summary>
    /// Turns the join line already in the conversation into one that also says the history came with it.
    /// Nothing is written when there is no such line - a group whose membership predates announcements -
    /// rather than inventing a join that was never announced.
    /// </summary>
    private async Task AnnounceHistorySharedAsync(ShareGroupHistoryCommand request, CancellationToken cancellationToken)
    {
        var announcement = await _chatGroupAnnouncementRepository.FindLatestJoinAsync(
            request.GroupId, request.RecipientUserId, cancellationToken);
        if (announcement is null || announcement.HistoryShared)
        {
            return;
        }

        announcement.MarkHistoryShared();
        await _chatGroupAnnouncementRepository.UpdateAsync(announcement, cancellationToken);
    }
}
