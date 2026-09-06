using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orbit.Api.LiveUpdates;

/// <summary>
/// One announcement as it crosses between API instances - see <see cref="PostgresLiveUpdateFanOut"/>.
///
/// It carries no content, for the same reason <see cref="Orbit.Core.LiveUpdates.ILiveUpdatePublisher"/>
/// carries none: the client answers every announcement by re-reading over the API it already uses, and
/// chat messages are end-to-end encrypted, so there is no plaintext here to carry even if it were
/// wanted. What crosses is a message name and a list of accounts to wake.
/// </summary>
internal sealed record LiveUpdateAnnouncement(
    [property: JsonPropertyName("origin")] Guid Origin,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("users")] IReadOnlyList<Guid> UserIds,
    [property: JsonPropertyName("arguments")] IReadOnlyList<JsonElement> Arguments)
{
    /// <summary>
    /// PostgreSQL refuses a NOTIFY payload over 8000 bytes, so a group large enough to exceed that is
    /// sent as several announcements rather than one that throws.
    ///
    /// The arithmetic behind the number: a Guid is 38 bytes as JSON and 39 with its comma, so 100 of
    /// them is 3900. Origin, message name and the property names come to under 200 more, which leaves
    /// most of the budget spare for <see cref="Arguments"/> - today one Guid at most, and the room is
    /// there so that adding to it later is not a silent 8001-byte failure in production.
    /// </summary>
    public const int MaxUserIdsPerAnnouncement = 100;

    /// <summary>
    /// The channel both halves agree on. LISTEN cannot take a parameter - a channel is an identifier,
    /// not a value - so this constant is interpolated into that statement and must stay a literal here
    /// rather than becoming anything a request could influence.
    /// </summary>
    public const string ChannelName = "orbit_live_updates";

    /// <summary>
    /// Splits an audience too large for one payload. Arguments ride on every part, since each part is a
    /// complete announcement to the accounts it names.
    /// </summary>
    public static IEnumerable<LiveUpdateAnnouncement> ForAudience(
        Guid origin, string message, IReadOnlyCollection<Guid> userIds, IReadOnlyList<object?> arguments)
    {
        var carried = arguments.Count == 0
            ? []
            : arguments.Select(argument => JsonSerializer.SerializeToElement(argument)).ToArray();

        return userIds
            .Chunk(MaxUserIdsPerAnnouncement)
            .Select(chunk => new LiveUpdateAnnouncement(origin, message, chunk, carried));
    }
}
