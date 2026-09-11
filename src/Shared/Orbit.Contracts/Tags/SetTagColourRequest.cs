namespace Orbit.Contracts.Tags;

/// <summary>
/// Gives <paramref name="Tag"/> this colour for the whole account, or takes its colour away when
/// <paramref name="Colour"/> is empty. "#rrggbb" otherwise - what a colour input gives.
/// </summary>
public sealed record SetTagColourRequest(string Tag, string Colour);
