using Orbit.Core.Abstractions;

namespace Orbit.Core.Users;

/// <summary>
/// Where a user last recorded themselves as being: coordinates, the address reverse geocoding resolved
/// for them if it managed to, and when it was taken. One point per user, replaced each time they record
/// a new one - Orbit keeps no trail of where someone has been, which is a deliberate limit rather than a
/// missing feature. Recording is always something the user does on purpose (see MapPage.razor); nothing
/// in Orbit reads the browser's position on its own.
///
/// Mirrors Orbit.Core.Calendar.EventLocation's shape, and validates the same way: a point off the globe
/// is a refused request rather than something stored and puzzled over later.
/// </summary>
public sealed record UserLocation
{
    public string? Address { get; }
    public double Latitude { get; }
    public double Longitude { get; }
    public DateTimeOffset RecordedAtUtc { get; }

    public UserLocation(string? address, double latitude, double longitude, DateTimeOffset recordedAtUtc)
    {
        if (latitude is < -90 or > 90)
        {
            throw new InvalidRequestException("A location's latitude must be between -90 and 90 degrees.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new InvalidRequestException("A location's longitude must be between -180 and 180 degrees.");
        }

        Address = address;
        Latitude = latitude;
        Longitude = longitude;
        RecordedAtUtc = recordedAtUtc;
    }
}
