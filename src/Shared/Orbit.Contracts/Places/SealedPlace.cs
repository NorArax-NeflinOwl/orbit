using Orbit.Contracts.Calendar;

namespace Orbit.Contracts.Places;

/// <summary>
/// What a sealed place holds inside its payload: what it is called, what was written about it, and where
/// it is. Everything the readable columns give up when a place is private - see Orbit.Core.Places.Place.
///
/// The point travels in here with the words. A place whose coordinates were still readable would be
/// sealed in name only, which is the opposite of what somebody sealing a place is asking for.
/// </summary>
public sealed record SealedPlace(string Name, string Description, EventLocationDto Where);
