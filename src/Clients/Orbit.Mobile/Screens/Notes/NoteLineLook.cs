using Orbit.Core.Notes;

namespace Orbit.Mobile.Screens.Notes;

/// <summary>
/// How a line of a note is drawn on the phone, for the two screens that draw one: the note itself
/// (<see cref="NoteLineRow"/>) and a note read through a link (Screens.Sharing.SharedLine). One place, so
/// a heading is the same heading whichever of them somebody is looking at.
///
/// Sizes and a mark rather than anything of MAUI's: this project is plain net10.0 on purpose (see the
/// note in its .csproj) and knows nothing about controls, so boldness leaves here as a bool and the page
/// turns it into a font - see NoteDetailPage.xaml.
/// </summary>
public static class NoteLineLook
{
    /// <summary>What an ordinary line is drawn at - the size every Entry and Label in the app already uses.</summary>
    public const double BodySize = 14;

    /// <summary>
    /// How large a line of this style is drawn. The note's own title field is 22, which is what a Title
    /// line is: the same weight of thing, so the two do not disagree on the same screen.
    /// </summary>
    public static double SizeOf(NoteLineStyle style) => style switch
    {
        NoteLineStyle.Title => 22,
        NoteLineStyle.Heading => 18,
        NoteLineStyle.Subheading => 16,
        _ => BodySize
    };

    /// <summary>Bold for a heading of any size, and nothing for everything else.</summary>
    public static bool IsBold(NoteLineStyle style) => style.IsAHeading();

    /// <summary>
    /// The mark at the head of a line of a list - a dot, a dash, or the line's number. Drawn beside the
    /// words rather than typed into them, so it is never part of what is stored and never carried by a
    /// copy of the line. Empty for everything that is not a list.
    /// </summary>
    /// <param name="number">
    /// What a numbered line shows - its place in the run of numbered lines above it, which the screen
    /// works out over all its lines at once (see NoteLineStyles.NumberOf).
    /// </param>
    public static string MarkOf(NoteLineStyle style, int number) => style switch
    {
        NoteLineStyle.Bulleted => "•",
        NoteLineStyle.Dashed => "–",
        NoteLineStyle.Numbered => $"{number}.",
        _ => string.Empty
    };

    /// <summary>
    /// The number to show beside each line, in order - 0 where there is none. Over all the lines at once,
    /// because a line's number is a fact about what is above it: nothing is stored, so the numbers can
    /// never disagree with where the lines actually are.
    /// </summary>
    public static IReadOnlyList<int> NumbersFor(IReadOnlyList<NoteLineStyle> styles)
    {
        var numbers = new int[styles.Count];
        var number = 0;
        for (var index = 0; index < styles.Count; index++)
        {
            number = styles[index] == NoteLineStyle.Numbered ? number + 1 : 0;
            numbers[index] = number;
        }

        return numbers;
    }
}
