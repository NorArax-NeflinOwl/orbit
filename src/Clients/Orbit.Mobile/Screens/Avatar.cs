namespace Orbit.Mobile.Screens;

/// <summary>
/// How somebody is drawn where there is no picture to draw them with: up to two initials on a circle
/// whose colour is theirs and stays theirs. Both halves are worked out the way Orbit.Web works them out
/// - see its AvatarHelper - because the same person has to read the same on both clients, and an avatar
/// is on every screen, so a rule of its own here would show up everywhere.
/// </summary>
/// <param name="Hue">
/// Degrees around the colour wheel, for whichever colour space the client draws in. Kept as the hue
/// rather than as a finished colour so that this stays free of any one client's drawing types.
/// </param>
public readonly record struct Avatar(string Initials, int Hue)
{
    public static Avatar Of(Guid id, string? displayName) => new(InitialsOf(displayName), HueOf(id));

    /// <summary>
    /// Up to two initials, for the one avatar drawn in the reader's own colour rather than in theirs -
    /// the account's own, in the navigation bar - which needs no hue.
    ///
    /// The one deliberate difference from the browser, which renders an empty name as "?": an avatar
    /// reading "?" looks like a fault rather than an unnamed account.
    /// </summary>
    public static string InitialsOf(string? displayName)
    {
        var words = (displayName ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return words switch
        {
            [] => string.Empty,
            [var only] => (only.Length >= 2 ? only[..2] : only[..1]).ToUpperInvariant(),
            [var first, var second, ..] => $"{first[..1]}{second[..1]}".ToUpperInvariant()
        };
    }

    /// <summary>
    /// The same hue the browser picks for this id. Masked rather than Math.Abs, which the browser uses:
    /// the two agree on every id, and one hash in four billion is int.MinValue, where Math.Abs throws.
    /// </summary>
    private static int HueOf(Guid id) => (id.GetHashCode() & int.MaxValue) % 360;
}
