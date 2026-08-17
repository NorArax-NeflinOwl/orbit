namespace Orbit.Core.Calendar;

/// <summary>
/// A place picked on a map for a calendar event: its geographic coordinates, and the human-readable
/// address resolved for them, if reverse geocoding succeeded - a location can still be set from just
/// its coordinates. Latitude and longitude always travel together with the address, so they're grouped
/// here instead of living as separate fields on <see cref="CalendarEventDetails"/>.
/// </summary>
public sealed record EventLocation(string? Address, double Latitude, double Longitude);
