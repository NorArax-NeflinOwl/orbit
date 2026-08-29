using System.Net.Http.Json;
using System.Text.Json;

namespace Orbit.Mobile.Api;

/// <summary>
/// Reads the reason the server gave for refusing a request. Orbit.Api answers every refusal it is
/// willing to explain with <c>{ "message": ... }</c> (see InvalidRequestExceptionHandler), and that
/// wording is already written for a person and knows more about the rule than any guess made here -
/// "A group needs at least one admin - promote someone else first" is not something the client should
/// try to reconstruct.
/// </summary>
public static class RefusalMessage
{
    /// <param name="fallback">Used when the body carries no message, so a reader never sees raw JSON.</param>
    public static async Task<string> ReadAsync(
        HttpResponseMessage response, string fallback, CancellationToken cancellationToken = default)
    {
        try
        {
            var refusal = await response.Content.ReadFromJsonAsync<ServerMessage>(cancellationToken);
            return string.IsNullOrWhiteSpace(refusal?.Message) ? fallback : refusal.Message;
        }
        catch (Exception exception) when (exception is JsonException or HttpRequestException)
        {
            return fallback;
        }
    }

    private sealed record ServerMessage(string? Message);
}
