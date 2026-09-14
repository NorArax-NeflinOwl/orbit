using System.Globalization;
using System.Text;

namespace Orbit.Core.Text;

/// <summary>
/// Matching words the way somebody searching means them rather than the way they typed them: case is
/// ignored, and so are the marks over and through a letter - so "zolw" finds "Żółw", "Zolw" finds it too,
/// and "ą" still finds "a".
///
/// Why this is a rule rather than a nicety: Orbit is written in Polish as much as in English, and the
/// marks are exactly the characters a phone keyboard makes somebody hold a key for. A search box that
/// insists on them is a search box that finds nothing until the third attempt, and the reader has no way
/// of telling a name that is not there from a name whose "ł" they did not type.
///
/// Both directions are folded, so it does not matter which side carries the marks: a reader who does type
/// "Żółw" still finds a row somebody else wrote as "Zolw". That is the half a simple "strip the search
/// text" would miss.
///
/// The fold is Unicode's own: a string is taken apart into letters and the marks on them
/// (<see cref="NormalizationForm.FormD"/>), the marks are dropped, and what is left is put back together.
/// It leaves a letter that is not a marked form of another alone - Polish "ł" is its own letter and stays
/// one, which is why <see cref="Equivalents"/> exists beside it.
/// </summary>
public static class LooseText
{
    /// <summary>
    /// Letters no amount of unpicking turns into a plain one, because they are not a plain letter with a
    /// mark on it - they are letters in their own right, drawn with a stroke through or a tail. Somebody
    /// searching types the plain letter for them all the same.
    ///
    /// Kept as a list rather than inferred: this is a statement about what a reader expects, not about
    /// Unicode, and adding a language means adding its letters here on purpose.
    /// </summary>
    private static readonly (char Written, char Typed)[] Equivalents =
    [
        ('ł', 'l'), ('Ł', 'L'),
        ('đ', 'd'), ('Đ', 'D'),
        ('ø', 'o'), ('Ø', 'O'),
        ('ß', 's'), ('æ', 'a'), ('Æ', 'A')
    ];

    /// <summary>
    /// <paramref name="text"/> with the marks taken off its letters and nothing else changed - the form
    /// two pieces of text are compared in. Case is left alone here and handled by the comparison, so this
    /// answers something a reader could still recognise.
    /// </summary>
    public static string Folded(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var plain = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            plain.Append(EquivalentOf(character));
        }

        var apart = plain.ToString().Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(apart.Length);
        foreach (var character in apart)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                folded.Append(character);
            }
        }

        return folded.ToString().Normalize(NormalizationForm.FormC);
    }

    private static char EquivalentOf(char character)
    {
        foreach (var (written, typed) in Equivalents)
        {
            if (character == written)
            {
                return typed;
            }
        }

        return character;
    }

    /// <summary>
    /// Whether <paramref name="text"/> holds <paramref name="wanted"/>, ignoring case and the marks on
    /// either side's letters. What every search box and every suggestion list asks.
    ///
    /// Nothing wanted is held by everything, which is what an empty search box means.
    /// </summary>
    public static bool Holds(string? text, string? wanted)
        => string.IsNullOrWhiteSpace(wanted)
            || Folded(text).Contains(Folded(wanted), StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    /// Whether two pieces of text are the same word to somebody reading them. Used where a list asks
    /// whether it already holds something rather than whether anything looks like it.
    /// </summary>
    public static bool Same(string? text, string? other)
        => string.Equals(Folded(text), Folded(other), StringComparison.CurrentCultureIgnoreCase);
}
