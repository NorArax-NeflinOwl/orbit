namespace Orbit.Web;

/// <summary>Small pieces of markup that have to be written out rather than typed as whitespace.</summary>
public static class Markup
{
    /// <summary>
    /// A space Razor will not throw away.
    ///
    /// Razor drops whitespace-only text nodes between two elements, so a sentence written next to a
    /// value or a link renders as one word - "Code sent toanna@example.com", "stays on thenotifications
    /// page". Written as an expression the space is content rather than layout, and survives.
    ///
    /// For two things laid out side by side rather than read as one sentence - a label and its badge, a
    /// title and its count - use a CSS gap instead. This is only for prose, and it is deliberately not a
    /// trailing space inside a translation, where nobody can see it and every language has to remember it.
    /// </summary>
    public const string Space = " ";
}
