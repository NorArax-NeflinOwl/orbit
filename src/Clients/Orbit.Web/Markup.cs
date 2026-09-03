using Microsoft.AspNetCore.Components;

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

    /// <summary>
    /// A value that might not be there, as markup a page can hand to a RenderFragment parameter - null
    /// when there is nothing to show. Written for PageHeader's Subtitle and Hint: both take a page's own
    /// markup rather than one fixed tag, but what a page has to say there is just as often a single line
    /// of plain text that only appears sometimes - a "Shared by" line, a hint that nothing has been
    /// written yet - and a page reaching for this instead of a conditional child tag is what keeps that
    /// line from appearing as an empty, still-spaced paragraph when there is nothing to say.
    /// </summary>
    public static RenderFragment? Optional(string? text)
        => string.IsNullOrEmpty(text) ? null : builder => builder.AddContent(0, text);
}
