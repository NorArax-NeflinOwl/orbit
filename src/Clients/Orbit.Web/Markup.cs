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

    /// <summary>
    /// The same, for a line somebody wrote themselves: the web addresses in it come back pressable -
    /// see <see cref="Services.LinksInText"/> and TextWithLinks.
    ///
    /// Its own helper rather than a change to <see cref="Optional"/>, which most callers hand a
    /// sentence Orbit composed - "Shared by Anna", "Nothing written yet". Those hold no addresses, and
    /// a general helper that quietly went looking for some in every line would be doing something its
    /// name does not say.
    ///
    /// Still null for nothing, which is the whole reason both of these exist: a subtitle given an empty
    /// fragment is still a subtitle, and draws an empty line that takes up its own space.
    /// </summary>
    public static RenderFragment? OptionalWithLinks(string? text)
        => string.IsNullOrEmpty(text)
            ? null
            : builder =>
            {
                builder.OpenComponent<Components.TextWithLinks>(0);
                builder.AddComponentParameter(1, nameof(Components.TextWithLinks.Text), text);
                builder.CloseComponent();
            };
}
