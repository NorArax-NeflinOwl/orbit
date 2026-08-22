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

    public async Task<IReadOnlyList<ContactDto>> GetContactsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<ContactDto>>("api/chat/contacts", cancellationToken) ?? [];

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
}
