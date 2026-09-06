using System.Text.RegularExpressions;

namespace Orbit.Web.Services;

/// <summary>
/// One piece of a description: either words, or a web address somebody wrote in the middle of them.
/// </summary>
/// <param name="Text">What is shown, exactly as it was written.</param>
/// <param name="Url">
/// Where it leads, or null for ordinary words. Always <c>http://</c> or <c>https://</c> - see
/// <see cref="LinksInText"/> for why that is a rule rather than a coincidence.
/// </param>
public sealed record TextRun(string Text, string? Url);

/// <summary>
/// Finds the web addresses in a description so they can be pressed instead of copied out by hand.
///
/// A splitter rather than something that produces HTML, and that is the whole security design: what
/// comes back is text and addresses, and the component that draws it hands both to Blazor, which
/// escapes them. Nothing here builds markup, so nothing here can be made to build markup - a
/// description is written by people, and one of them may be somebody who shared a note with you.
///
/// Only <c>http://</c> and <c>https://</c> become links, plus a bare <c>www.</c>, which is written far
/// too often to ignore and is given <c>https://</c> when it is followed. Every other scheme is left as
/// words on purpose: <c>javascript:</c> and <c>data:</c> in an href are the two that turn a description
/// into a way of running something, and a rule that lists what is allowed cannot be widened by
/// accident the way one that lists what is forbidden can.
/// </summary>
public static class LinksInText
{
    /// <summary>
    /// A run of non-space characters starting with a scheme or with "www.". Not preceded by a letter,
    /// digit or "@", so the tail of an email address and the middle of a longer word are left alone.
    /// Deliberately loose about what may follow: an address can hold almost anything, and where it ends
    /// is decided below by what a sentence puts after it rather than by a list of legal characters.
    /// </summary>
    private static readonly Regex Candidate = new(
        @"(?<![\w@])(https?://|www\.)[^\s<>""']+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// What a sentence puts after an address rather than inside one. A full stop ends the sentence, not
    /// the link; a closing bracket usually closes the one the address was put in.
    /// </summary>
    private const string SentencePunctuation = ".,;:!?'\"”’)]}";

    /// <summary>
    /// The description split into what to print and what to link. A description with no address in it
    /// comes back as one run, which is the ordinary case and costs one match attempt.
    /// </summary>
    public static IReadOnlyList<TextRun> Split(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var runs = new List<TextRun>();
        var readUpTo = 0;
        foreach (var match in Candidate.Matches(text).Cast<Match>())
        {
            var address = TrimmedOfWhatFollowsIt(match.Value);
            if (address.Length == 0)
            {
                continue;
            }

            if (match.Index > readUpTo)
            {
                runs.Add(new TextRun(text[readUpTo..match.Index], Url: null));
            }

            runs.Add(new TextRun(address, Href(address)));
            readUpTo = match.Index + address.Length;
        }

        if (readUpTo < text.Length)
        {
            runs.Add(new TextRun(text[readUpTo..], Url: null));
        }

        return runs;
    }

    /// <summary>Whether this description holds an address at all - what a caller asks before drawing one run per piece.</summary>
    public static bool HasAny(string? text) => text is not null && Candidate.IsMatch(text);

    /// <summary>
    /// The address without the sentence that surrounds it. Trailing punctuation is dropped, except a
    /// closing bracket that an opening one inside the address accounts for - "…/wiki/Orbit_(disambiguation)"
    /// is one address, while "(see https://example.com)" is an address inside a bracket.
    /// </summary>
    private static string TrimmedOfWhatFollowsIt(string candidate)
    {
        var end = candidate.Length;
        while (end > 0 && SentencePunctuation.Contains(candidate[end - 1]))
        {
            if (candidate[end - 1] == ')' && Counted(candidate[..end], '(') >= Counted(candidate[..end], ')'))
            {
                break;
            }

            end -= 1;
        }

        var trimmed = candidate[..end];

        // "https://" and "www." on their own are somebody typing, not an address. Left as words rather
        // than linked to a host that does not exist.
        return trimmed.EndsWith("//", StringComparison.Ordinal) || trimmed.EndsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : trimmed;
    }

    private static int Counted(string text, char character) => text.Count(letter => letter == character);

    /// <summary>
    /// What the link points at. A bare "www.orbit.example" is given https, which is the scheme it would
    /// have been written with; everything else already carries its own, and no other scheme reaches
    /// here - see the class comment.
    /// </summary>
    private static string Href(string address)
        => address.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? $"https://{address}" : address;
}
