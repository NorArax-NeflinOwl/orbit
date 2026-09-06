using System.Text.Json;
using System.Text.Json.Serialization;
using Orbit.Api.Instances;

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
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("users")] IReadOnlyList<Guid> UserIds,
    [property: JsonPropertyName("arguments")] IReadOnlyList<JsonElement> Arguments)
{
    public const string Channel = "orbit_live_updates";

    /// <summary>
    /// An audience is the one thing here that has no upper bound - a group conversation is one
    /// announcement to every member - so it is split rather than allowed to outgrow what NOTIFY accepts.
    ///
    /// The arithmetic: a Guid is 38 bytes as JSON and 39 with its comma, so 100 of them is 3900. The
    /// message name, the property names and the envelope around it come to a few hundred more, which
    /// leaves most of the budget spare for <see cref="Arguments"/> - today one Guid at most, and the
    /// room is there so that adding to it later is not a silent failure in production.
    /// </summary>
    public const int MaxUserIdsPerAnnouncement = 100;

    /// <summary>
    /// Splits an audience too large for one payload. Arguments ride on every part, since each part is a
    /// complete announcement to the accounts it names.
    /// </summary>
    public static IEnumerable<LiveUpdateAnnouncement> ForAudience(
        string message, IReadOnlyCollection<Guid> userIds, IReadOnlyList<object?> arguments)
    {
        var carried = arguments.Count == 0
            ? []
            : arguments.Select(argument => JsonSerializer.SerializeToElement(argument)).ToArray();

        return userIds
            .Chunk(MaxUserIdsPerAnnouncement)
            .Select(chunk => new LiveUpdateAnnouncement(message, chunk, carried));
    }
}
