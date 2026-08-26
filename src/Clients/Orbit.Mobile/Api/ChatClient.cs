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
