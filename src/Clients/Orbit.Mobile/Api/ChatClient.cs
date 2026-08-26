using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Chat;

namespace Orbit.Mobile.Api;

/// <summary>What the server did with a message the app tried to send.</summary>
public enum SendMessageOutcome
{
    Sent,

    /// <summary>
    /// The recipient hasn't approved this conversation yet. A real state rather than an error: the app
    /// should stop offering to send until they do.
    /// </summary>
    NotApproved,

    /// <summary>The recipient no longer exists. Nothing queued against them can ever succeed.</summary>
    RecipientGone
}

public sealed record SendMessageResult(SendMessageOutcome Outcome, ChatMessageDto? Message = null);

/// <summary>
/// What the server did with a change to a group's membership.
/// </summary>
/// <param name="Refusal">
/// The rule that was broken, in the server's own words - "A group needs at least one admin - promote
/// someone else first", "You can only add people you already have a chat with". Null when the change
/// went through, or when the group is simply not visible to this account, which the server declines to
/// explain at all: membership decides who may even see a group, so distinguishing "no such group" from
/// "not yours" would leak the difference.
/// </param>
public sealed record GroupMemberChangeResult(bool Done, string? Refusal = null)
{
    public static readonly GroupMemberChangeResult Applied = new(Done: true);

    /// <summary>The group is gone, or this account is not in it - the server answers the same to both.</summary>
    public static readonly GroupMemberChangeResult NotVisible = new(Done: false);
}

/// <summary>What the server did with a group message the app tried to send.</summary>
public enum GroupSendOutcome
{
    Sent,

    /// <summary>
    /// The group is gone, or the sender is no longer in it. Membership decides both, and the server
    /// deliberately answers the same way to either - see IChatGroupRepository. Nothing queued for that
    /// group can ever succeed.
    /// </summary>
    NoLongerAMember,

    /// <summary>
    /// The fan-out did not match the group's membership: somebody joined or left between the app reading
    /// the member list and posting. Worth trying again, because the next attempt re-reads the list.
    /// </summary>
    MembershipChanged
}

/// <summary>
/// The chat half of the API. Everything here carries ciphertext only - the plaintext never leaves the
/// device, and Orbit.Api stores and relays what it cannot read.
/// </summary>
public sealed class ChatClient
{
    private readonly HttpClient _httpClient;

    public ChatClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<IReadOnlyList<ContactDto>> GetContactsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<ContactDto>>("api/chat/contacts", cancellationToken) ?? [];

    /// <summary>
    /// The conversation with one person. <paramref name="sinceUtc"/> asks for only what arrived after a
    /// point, which is how the app catches up without re-downloading a history it already holds.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessageDto>> GetConversationAsync(
        Guid otherUserId, DateTimeOffset? sinceUtc = null, CancellationToken cancellationToken = default)
    {
        var path = $"api/chat/messages/{otherUserId}";
        if (sinceUtc is { } since)
        {
            path += $"?sinceUtc={Uri.EscapeDataString(since.UtcDateTime.ToString("O"))}";
        }

        return await _httpClient.GetFromJsonAsync<IReadOnlyList<ChatMessageDto>>(path, cancellationToken) ?? [];
    }

    /// <summary>
    /// Marks everything the other party has sent as read, as of now. Called while their conversation is
    /// actually in front of somebody - that is the whole definition of "read" Orbit has.
    /// </summary>
    public async Task MarkConversationAsReadAsync(Guid otherUserId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsync($"api/chat/messages/{otherUserId}/read", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// How far the other party has read: the send-time of the newest message of the caller's that they
    /// have seen, or null if none. One timestamp for the whole conversation rather than a flag per
    /// message - see GetReadUpToUtcAsync.
    /// </summary>
    public async Task<DateTimeOffset?> GetReadReceiptAsync(Guid otherUserId, CancellationToken cancellationToken = default)
    {
        var receipt = await _httpClient.GetFromJsonAsync<ReadReceiptDto>(
            $"api/chat/messages/{otherUserId}/read-receipt", cancellationToken);

        return receipt?.ReadUpToUtc;
    }

    /// <summary>
    /// Lets the party who did not start a conversation allow it. Until they do, the server refuses
    /// anything they try to send - see SendMessageCommandHandler - so this is what unblocks replying.
    /// </summary>
    /// <returns>False when there is no such request to approve, which a stale screen can produce.</returns>
    public async Task<bool> ApproveConversationAsync(Guid otherUserId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(
            $"api/chat/conversations/{otherUserId}/approve", content: null, cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>
    /// Puts somebody into a group. Refused unless the caller is an admin and already has a conversation
    /// with them - a group must not become a way to reach somebody who never agreed to hear from you.
    /// </summary>
    public Task<GroupMemberChangeResult> AddGroupMemberAsync(
        Guid groupId, Guid userId, CancellationToken cancellationToken = default)
        => ChangeMembershipAsync(
            new HttpRequestMessage(HttpMethod.Post, $"api/chat/groups/{groupId}/members")
            {
                Content = JsonContent.Create(new AddChatGroupMemberRequest(userId))
            },
            "They could not be added to this group.", cancellationToken);

    /// <summary>
    /// Takes somebody out of a group, which an admin may also do to themselves - leaving. Refused when
    /// it would strip the group of its last admin.
    /// </summary>
    public Task<GroupMemberChangeResult> RemoveGroupMemberAsync(
        Guid groupId, Guid userId, CancellationToken cancellationToken = default)
        => ChangeMembershipAsync(
            new HttpRequestMessage(HttpMethod.Delete, $"api/chat/groups/{groupId}/members/{userId}"),
            "They could not be removed from this group.", cancellationToken);

    /// <param name="role">"Admin" or "Member" - see Orbit.Core.Chat.Groups.ChatGroupRole.</param>
    public Task<GroupMemberChangeResult> ChangeGroupMemberRoleAsync(
        Guid groupId, Guid userId, string role, CancellationToken cancellationToken = default)
        => ChangeMembershipAsync(
            new HttpRequestMessage(HttpMethod.Put, $"api/chat/groups/{groupId}/members/{userId}/role")
            {
                Content = JsonContent.Create(new ChangeChatGroupMemberRoleRequest(role))
            },
            "That role could not be changed.", cancellationToken);

    /// <summary>
    /// All three membership changes answer the same way, so they are sent the same way: no content on
    /// success, 404 for a group this account cannot see, and 400 naming the rule that stopped it.
    /// </summary>
    private async Task<GroupMemberChangeResult> ChangeMembershipAsync(
        HttpRequestMessage request, string fallbackRefusal, CancellationToken cancellationToken)
    {
        using (request)
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode is HttpStatusCode.NotFound)
            {
                return GroupMemberChangeResult.NotVisible;
            }

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden)
            {
                return new GroupMemberChangeResult(
                    Done: false, await RefusalMessage.ReadAsync(response, fallbackRefusal, cancellationToken));
            }

            response.EnsureSuccessStatusCode();
            return GroupMemberChangeResult.Applied;
        }
    }

    /// <summary>Every group the signed-in user is in, with its current membership.</summary>
    public async Task<IReadOnlyList<ChatGroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<ChatGroupDto>>("api/chat/groups", cancellationToken) ?? [];

    public async Task<Guid> CreateGroupAsync(CreateChatGroupRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/chat/groups", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken);
    }

    /// <summary>
    /// The whole group conversation. Unlike the one-to-one endpoint this takes no "since", so it is the
    /// full history every time; the store replaces by message id, so re-reading costs bandwidth rather
    /// than duplicates.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessageDto>> GetGroupConversationAsync(
        Guid groupId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<ChatMessageDto>>(
            $"api/chat/groups/{groupId}/messages", cancellationToken) ?? [];

    /// <summary>
    /// Posts one message as one ciphertext per other member. The server checks the set against the
    /// group's current membership and refuses anything that doesn't match exactly, which is what
    /// <see cref="GroupSendOutcome.MembershipChanged"/> reports.
    /// </summary>
    public async Task<GroupSendOutcome> SendGroupMessageAsync(
        Guid groupId, IReadOnlyList<GroupMessageCopyDto> copies, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/chat/groups/{groupId}/messages", new SendGroupMessageRequest(copies), cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return GroupSendOutcome.NoLongerAMember;
            case HttpStatusCode.BadRequest:
                return GroupSendOutcome.MembershipChanged;
            default:
                response.EnsureSuccessStatusCode();
                return GroupSendOutcome.Sent;
        }
    }

    /// <summary>
    /// Removes a message for everyone, not just for the caller. One copy's id is enough for a group
    /// message: the server removes every copy of the same posting - see DeleteChatMessageCommandHandler.
    /// </summary>
    /// <returns>False when it is already gone, or was never the caller's to remove.</returns>
    public async Task<bool> DeleteMessageAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync($"api/chat/messages/{messageId}", cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>Rewrites a one-to-one message. False when it is gone or was somebody else's to edit.</summary>
    public async Task<bool> EditMessageAsync(
        Guid messageId, EditMessageRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync($"api/chat/messages/{messageId}", request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>
    /// Rewrites one group message. The same fan-out as sending, because every copy is separately
    /// encrypted - leaving one behind would show different members different words.
    /// </summary>
    public async Task<GroupSendOutcome> EditGroupMessageAsync(
        Guid groupId, Guid groupMessageId, IReadOnlyList<GroupMessageCopyDto> copies,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"api/chat/groups/{groupId}/messages/{groupMessageId}", new SendGroupMessageRequest(copies), cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return GroupSendOutcome.NoLongerAMember;
            case HttpStatusCode.BadRequest:
                return GroupSendOutcome.MembershipChanged;
            default:
                response.EnsureSuccessStatusCode();
                return GroupSendOutcome.Sent;
        }
    }

    public async Task<SendMessageResult> SendAsync(
        SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/chat/messages", request, cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.Forbidden:
                return new SendMessageResult(SendMessageOutcome.NotApproved);
            case HttpStatusCode.NotFound:
                return new SendMessageResult(SendMessageOutcome.RecipientGone);
            default:
                response.EnsureSuccessStatusCode();
                return new SendMessageResult(
                    SendMessageOutcome.Sent,
                    await response.Content.ReadFromJsonAsync<ChatMessageDto>(cancellationToken));
        }
    }
}
