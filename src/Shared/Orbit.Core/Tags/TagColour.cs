using System.Text.RegularExpressions;

namespace Orbit.Core.Tags;

/// <summary>
/// The colour one of an account's tags is drawn in, wherever it appears - on a note's card, a task list's,
/// the dashboard, the phone's rows. One per tag per account: the colour belongs to the word, not to any
/// one item carrying it, so colouring "work" on one note colours it on every note and list.
///
/// An account setting, kept on the server in the clear - see info/functionality.md ("Tags and their
/// colours"). A tag only ever gets a row here because somebody chose a colour for it; nothing writes one
/// on its own. The honest consequence is written down there: a tag used only on private items, once
/// coloured, is named here readably even though the items carrying it are sealed.
/// </summary>
/// <param name="Tag">The tag as it was written when its colour was last set.</param>
/// <param name="Colour">A CSS colour, "#rrggbb" in lower case.</param>
public sealed partial record TagColour(string Tag, string Colour)
{
    /// <summary>Whether this is a colour the store keeps: what a colour input gives, and nothing else.</summary>
    public static bool IsAColour(string colour) => HexColour().IsMatch(colour);

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColour();
}
