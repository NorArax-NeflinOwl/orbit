using System.Net.Http.Json;
using Orbit.Contracts.Chat;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/chat endpoints, keeping HTTP and JSON details out of the pages.
/// Never touches encryption itself - callers pass already-encrypted ciphertext in and get already-
/// encrypted ciphertext back (see OwnEncryptionKeyProvider and wwwroot/js/e2eeChat.js).
/// </summary>
public sealed class ChatApiClient
{
    private readonly HttpClient _httpClient;

    public ChatApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
        var response = await _httpClient.PostAsJsonAsync("api/chat/messages", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ChatMessageDto>(cancellationToken: cancellationToken))!;
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
}
