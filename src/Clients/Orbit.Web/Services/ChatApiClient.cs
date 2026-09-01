using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Chat;
using Orbit.Core.Abstractions;
using Orbit.Web.Services.Logging;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/chat endpoints, keeping HTTP and JSON details out of the pages.
/// Never touches encryption itself - callers pass already-encrypted ciphertext in and get already-
/// encrypted ciphertext back (see OwnEncryptionKeyProvider and wwwroot/js/e2eeChat.js).
/// </summary>
public sealed class ChatApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    // logger defaults to a no-op instance rather than being required, so existing call sites (including
    // every test that constructs this with just an HttpClient) keep compiling unchanged; only the
    // DI-resolved instance registered in Program.cs actually logs anywhere.
    public ChatApiClient(HttpClient httpClient, ILogger<ChatApiClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger ?? NullLogger<ChatApiClient>.Instance;
    }

    /// <summary>
    /// Empty rather than an exception when this account has not unlocked chat (see
    /// PermissionPolicies in Orbit.Api). Half the app asks this question in passing - a share picker, a
    /// dashboard card - and a refusal there is not a failure to report, it is the answer: there is
    /// nobody to show. Left throwing, one 403 in an editor's OnInitializedAsync took down the whole
    /// renderer.
    /// </summary>
    public async Task<IReadOnlyList<ContactDto>> GetContactsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ContactDto>>("api/chat/contacts", cancellationToken) ?? [];
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            return [];
        }
    }

    /// <summary>
    /// When sinceUtc is given, only returns messages strictly after it - used to poll for new messages
    /// in an already-open chat window instead of re-fetching the whole conversation every few seconds.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessageDto>> GetConversationAsync(
        Guid otherUserId, DateTimeOffset? sinceUtc, CancellationToken cancellationToken = default)
    {
        var url = sinceUtc is null
            ? $"api/chat/messages/{otherUserId}"
            : $"api/chat/messages/{otherUserId}?sinceUtc={Uri.EscapeDataString(sinceUtc.Value.ToString("O"))}";

        return await _httpClient.GetFromJsonAsync<List<ChatMessageDto>>(url, cancellationToken) ?? [];
    }

    public async Task<ChatMessageDto> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/chat/messages", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var message = (await response.Content.ReadFromJsonAsync<ChatMessageDto>(cancellationToken: cancellationToken))!;
            _logger.LogActionCompleted(ClientActionCategory.SendMessage, "Send chat message");
            return message;
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.SendMessage, "Send chat message", exception);
            throw;
        }
    }

    /// <summary>
    /// Re-encrypts and overwrites an already-sent message's content - only its original sender may do
    /// this (Orbit.Api returns 403 otherwise; Chat.razor only ever offers "Edit" on the sender's own
    /// bubbles, so that should never actually happen from the UI).
    /// </summary>
    public async Task<ChatMessageDto> EditMessageAsync(Guid messageId, EditMessageRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/chat/messages/{messageId}", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var message = (await response.Content.ReadFromJsonAsync<ChatMessageDto>(cancellationToken: cancellationToken))!;
            _logger.LogActionCompleted(ClientActionCategory.Edit, "Edit chat message");
            return message;
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.Edit, "Edit chat message", exception);
            throw;
        }
    }

    /// <summary>Marks every message otherUserId sent to the caller as read as of now.</summary>
    public async Task MarkConversationAsReadAsync(Guid otherUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsync($"api/chat/messages/{otherUserId}/read", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>How far otherUserId has read into the messages the caller sent them, or null if none yet.</summary>
    public async Task<DateTimeOffset?> GetReadReceiptAsync(Guid otherUserId, CancellationToken cancellationToken = default)
    {
        var receipt = await _httpClient.GetFromJsonAsync<ReadReceiptDto>($"api/chat/messages/{otherUserId}/read-receipt", cancellationToken);
        return receipt?.ReadUpToUtc;
    }

    /// <summary>Null means the caller and otherUserId have never exchanged a message, so nothing is gated.</summary>
    public async Task<ChatConversationAccessDto?> GetConversationAccessAsync(Guid otherUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/chat/conversations/{otherUserId}/access", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatConversationAccessDto>(cancellationToken: cancellationToken);
    }

    /// <summary>Allows the caller to chat with otherUserId, who started a conversation the caller hasn't approved yet.</summary>
    public async Task ApproveConversationAsync(Guid otherUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/chat/conversations/{otherUserId}/approve", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // Groups. A group's messages are ordinary per-recipient rows behind the scenes (see
    // EncryptedChatMessageSender.SendToGroupAsync), so only the addressing differs from one-to-one chat.

    /// <summary>
    /// Empty rather than an exception when this account has not unlocked group chat (see
    /// PermissionPolicies in Orbit.Api). Half the app asks this question in passing - a share picker, a
    /// dashboard card - and a refusal there is not a failure to report, it is the answer: there is
    /// nobody to show. Left throwing, one 403 in an editor's OnInitializedAsync took down the whole
    /// renderer.
    /// </summary>
    public async Task<IReadOnlyList<ChatGroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ChatGroupDto>>("api/chat/groups", cancellationToken) ?? [];
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            return [];
        }
    }

    public async Task<Guid> CreateGroupAsync(string name, IReadOnlyList<Guid> memberUserIds, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/chat/groups", new CreateChatGroupRequest(name, memberUserIds), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetGroupConversationAsync(Guid groupId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<ChatMessageDto>>($"api/chat/groups/{groupId}/messages", cancellationToken) ?? [];

    /// <summary>
    /// The group's "somebody joined" lines - read separately from its messages, since the two are
    /// different shapes (see the announcements route in ChatEndpoints).
    /// </summary>
    public async Task<IReadOnlyList<ChatGroupAnnouncementDto>> GetGroupAnnouncementsAsync(
        Guid groupId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<ChatGroupAnnouncementDto>>(
            $"api/chat/groups/{groupId}/announcements", cancellationToken) ?? [];

    /// <summary>
    /// Hands the group's past to a member who joined after it happened, already re-encrypted for them.
    /// Answers with how many copies were stored, which can be fewer than were offered - see the history
    /// route in ChatEndpoints.
    /// </summary>
    public async Task<int> ShareGroupHistoryAsync(
        Guid groupId, Guid recipientUserId, IReadOnlyList<SharedHistoryCopyDto> copies,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/chat/groups/{groupId}/history", new ShareGroupHistoryRequest(recipientUserId, copies), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>(cancellationToken);
    }

    public async Task SendGroupMessageAsync(
        Guid groupId, IReadOnlyList<GroupMessageCopyDto> copies, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/chat/groups/{groupId}/messages", new SendGroupMessageRequest(copies), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Rewrites every copy of one group message. Takes the whole fan-out again, for the same reason
    /// sending does - the server holds ciphertext it cannot open, so only this browser can produce the
    /// new text for each member. False when the message is gone or was somebody else's to edit.
    /// </summary>
    public async Task<bool> EditGroupMessageAsync(
        Guid groupId, Guid groupMessageId, IReadOnlyList<GroupMessageCopyDto> copies, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/chat/groups/{groupId}/messages/{groupMessageId}", new SendGroupMessageRequest(copies), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>Who one group message reached, and which of them have read it - see GroupMessageReceiptDto.</summary>
    public async Task<IReadOnlyList<GroupMessageReceiptDto>> GetGroupMessageReceiptsAsync(
        Guid groupId, Guid groupMessageId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<GroupMessageReceiptDto>>(
            $"api/chat/groups/{groupId}/messages/{groupMessageId}/receipts", cancellationToken) ?? [];

    /// <summary>
    /// Marks everything addressed to this reader in the group as read. Called while the group is open,
    /// the same coarse stand-in the one-to-one conversation uses.
    /// </summary>
    public async Task MarkGroupConversationAsReadAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsync($"api/chat/groups/{groupId}/read", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Puts a one-to-one conversation away, or brings it back. Only for this reader - the other party
    /// keeps theirs where it was, which is why the server takes no "archive for everybody".
    /// </summary>
    public async Task SetConversationArchivedAsync(
        Guid otherUserId, bool isArchived, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/chat/conversations/{otherUserId}/archived", new SetArchivedRequest(isArchived), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>The same for a group, and equally only for this reader - nobody else's list moves.</summary>
    public async Task SetGroupArchivedAsync(Guid groupId, bool isArchived, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/chat/groups/{groupId}/archived", new SetArchivedRequest(isArchived), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddGroupMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/chat/groups/{groupId}/members", new AddChatGroupMemberRequest(userId), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveGroupMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/chat/groups/{groupId}/members/{userId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ChangeGroupMemberRoleAsync(Guid groupId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/chat/groups/{groupId}/members/{userId}/role", new ChangeChatGroupMemberRoleRequest(role), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Deletes a message for everyone. Returns false when the caller isn't allowed to - see DeleteChatMessageCommandHandler.</summary>
    public async Task<bool> DeleteMessageAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/chat/messages/{messageId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }
}
