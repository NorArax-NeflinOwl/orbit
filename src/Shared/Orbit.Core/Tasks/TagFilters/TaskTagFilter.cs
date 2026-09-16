using Orbit.Core.Tags;

namespace Orbit.Core.Tasks.TagFilters;

/// <summary>
/// A filter somebody made for the dashboard's Tasks card out of their tags: the lists tagged with any of
/// these words, or - when <see cref="MatchesAll"/> - with every one of them. Made on the tasks page and
/// chosen from the card's own menu, on both clients.
///
/// An account setting kept on the server, beside the tag colours (see TagColour): a filter made in a
/// browser is there on the phone. It reads every list the account has, whatever folder it is in and
/// whether or not it is finished or put away - it is a way of finding lists by what they are about, and
/// a folder is a different answer to a different question.
///
/// It has no name of its own. It is called by what it looks for ("home or shopping"), which is what a
/// reader would have typed as its name anyway, and a name that could drift from the tags would be a
/// second thing to keep true.
/// </summary>
/// <param name="Tags">The words, tidied as a list's own tags are - see TagNames.Tidy.</param>
/// <param name="MatchesAll">Every word has to be on the list, rather than any one of them - the default.</param>
public sealed record TaskTagFilter(Guid Id, IReadOnlyList<string> Tags, bool MatchesAll, DateTimeOffset CreatedAtUtc)
{
    /// <summary>
    /// Whether a list carrying <paramref name="listTags"/> is one this filter finds. Compared the way a
    /// tag is known everywhere (TagNames.KeyOf), so "Home" finds a list tagged "home".
    /// </summary>
    public bool Matches(IEnumerable<string> listTags)
    {
        var carried = listTags.Select(TagNames.KeyOf).ToHashSet();
        var wanted = Tags.Select(TagNames.KeyOf);
        return MatchesAll ? wanted.All(carried.Contains) : wanted.Any(carried.Contains);
    }

    /// <summary>
    /// What the filter is called on a menu: its words joined by the reader's own "or" or "and", in the
    /// order they were chosen.
    /// </summary>
    public string Describe(string or, string and) => string.Join($" {(MatchesAll ? and : or)} ", Tags);
}
