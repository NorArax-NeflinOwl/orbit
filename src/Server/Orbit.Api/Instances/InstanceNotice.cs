using System.Text.Json.Serialization;

namespace Orbit.Api.Instances;

/// <summary>
/// What actually crosses between instances: who sent it, and the notice itself as the sender wrote it.
///
/// The origin is the envelope's whole reason for existing. NOTIFY comes back to the sender, and the
/// sender has already acted locally, so without something to compare against every instance would act
/// twice on its own notices - once directly and once off the wire. Keeping it out here rather than
/// inside each kind of notice means no future one can forget it.
/// </summary>
internal sealed record InstanceNotice(
    [property: JsonPropertyName("origin")] Guid Origin,
    [property: JsonPropertyName("body")] string Body)
{
    /// <summary>
    /// PostgreSQL refuses a NOTIFY payload over 8000 bytes. Senders that can produce an unbounded body -
    /// today only a live update to a large audience - split it themselves; this is the budget they split
    /// against, less what the envelope around them costs.
    /// </summary>
    public const int MaxPayloadBytes = 8000;

    /// <summary>Origin, quotes, property names and braces. Measured generously and on purpose.</summary>
    public const int EnvelopeBytes = 120;
}
