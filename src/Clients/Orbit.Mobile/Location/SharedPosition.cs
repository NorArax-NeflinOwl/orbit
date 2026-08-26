using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orbit.Mobile.Location;

/// <summary>
/// What a shared position carries, and the shape it is sealed in.
///
/// <b>The property names are part of the wire format.</b> Orbit.Web serialises this with
/// <c>JsonSerializer.Serialize(position)</c> and no options, which writes the member names as they are
/// spelled - so this must stay PascalCase and keep these exact names, or a position shared from a
/// browser opens on the phone as four nulls. Nothing in the request says so: the server holds ciphertext
/// and cannot tell either side that they disagree.
/// </summary>
public sealed record SharedPosition(double Latitude, double Longitude, string? Address, DateTimeOffset RecordedAtUtc)
{
    public string ToJson() => JsonSerializer.Serialize(this, SharedPositionSerializerContext.Default.SharedPosition);

    /// <summary>Null when the plaintext was not a position at all - see the note about names above.</summary>
    public static SharedPosition? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, SharedPositionSerializerContext.Default.SharedPosition);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Source-generated, and deliberately without a naming policy so the names go out exactly as declared -
/// see <see cref="SharedPosition"/> for why that matters.
/// </summary>
[JsonSerializable(typeof(SharedPosition))]
internal sealed partial class SharedPositionSerializerContext : JsonSerializerContext;
