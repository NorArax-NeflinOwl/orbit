namespace Orbit.Web.Services;

/// <summary>
/// The part of the browser's history this app has seen for itself: every address it has stood on since
/// it started, oldest first, with the one on screen last. The pure half of <see cref="NavigationTrail"/>,
/// kept apart from the navigation manager and the JS call so the rules can be tested on their own.
///
/// The browser does not say which kind of move a navigation was - a new page pushed, the current one
/// replaced, or Back and Forward walking the entries already there. So this remembers what it asked for
/// itself (<see cref="Leave"/>), and reads anything else by one rule: <b>arriving at an address still on
/// the trail, below the page on screen, is going back to it</b> - one entry with Back, or several through
/// the browser's history menu - and every other arrival is a new page.
///
/// That rule can be wrong: following a link to a page still on the trail looks exactly like going back
/// to it. The trail then holds less than the browser does, and only a screen reached that way and then
/// finished onto the entry the trail believes is behind it is affected - it steps back onto another of
/// Orbit's pages. The opposite mistake is the one that matters and the one this avoids: reading a jump
/// back as a new page would leave the trail believing in entries the browser has already gone below, and
/// stepping back onto one of those could leave the app. Stepping back is the only move that can, and it
/// is only made onto an entry this has seen.
///
/// Starts with the one address the app was opened on. A page reached by typing its address, a reload, or
/// a link from another site has nothing of Orbit's behind it, so finishing there replaces rather than
/// stepping back out of Orbit altogether.
/// </summary>
public sealed class InAppHistory
{
    private readonly List<string> _entries;

    /// <summary>
    /// What the next arrival should be when it is one this trail asked for - see <see cref="Leave"/>.
    /// Null while nothing is outstanding.
    /// </summary>
    private Expectation? _expected;

    public InAppHistory(string startingAt)
    {
        _entries = [startingAt];
    }

    /// <summary>Oldest first; the last one is the page on screen.</summary>
    public IReadOnlyList<string> Entries => _entries;

    /// <summary>
    /// The browser is now on <paramref name="location"/>. Returns an address to replace it with at once,
    /// when this arrival was the first half of a finish that needs a second (see <see cref="Leave"/>), and
    /// null otherwise.
    /// </summary>
    public string? Arrived(string location)
    {
        var expected = _expected;
        _expected = null;

        if (expected is not null && expected.Location == location)
        {
            _entries.RemoveRange(_entries.Count - expected.StepsBack, expected.StepsBack);
            _entries[^1] = location;
            if (expected.ThenReplaceWith is not { } next)
            {
                return null;
            }

            _expected = new Expectation(StepsBack: 0, next, ThenReplaceWith: null);
            return next;
        }

        // Below the page on screen only: the same address again is a second entry for it, which is what
        // navigating to the page already open does.
        if (_entries.Count >= 2 && _entries.LastIndexOf(location, _entries.Count - 2) is var goneBackTo and >= 0)
        {
            _entries.RemoveRange(goneBackTo + 1, _entries.Count - goneBackTo - 1);
            return null;
        }

        _entries.Add(location);
        return null;
    }

    /// <summary>
    /// How to leave the page on screen for <paramref name="destination"/> without leaving it behind in
    /// the history: step back onto the destination when it is the entry just before, and replace the page
    /// with it otherwise. Either way pressing Back afterwards does not reopen what was just finished - for
    /// a form that had just created something, that is a second copy of it one press away.
    ///
    /// <paramref name="leavingEverythingUnder"/> is for a thing that has just been deleted: every entry on
    /// top that belongs to it (its own page and the form opened from it, say) is left as well, since each
    /// would now open on "no longer exists". The oldest of them is stepped back onto and replaced.
    ///
    /// Records what the browser should arrive at, so <see cref="Arrived"/> reads the move as this rather
    /// than as a new page.
    /// </summary>
    public FinishStep Leave(string destination, string? leavingEverythingUnder = null)
    {
        var leaving = 1;
        if (leavingEverythingUnder is not null)
        {
            while (leaving < _entries.Count && IsUnder(_entries[^(leaving + 1)], leavingEverythingUnder))
            {
                leaving++;
            }
        }

        if (_entries.Count > leaving && Reaches(_entries[^(leaving + 1)], destination))
        {
            _expected = new Expectation(leaving, _entries[^(leaving + 1)], ThenReplaceWith: null);
            return new FinishStep(leaving, ReplaceWith: null);
        }

        var stepsBack = leaving - 1;
        _expected = stepsBack == 0
            ? new Expectation(StepsBack: 0, destination, ThenReplaceWith: null)
            : new Expectation(stepsBack, _entries[^(stepsBack + 1)], destination);
        return new FinishStep(stepsBack, destination);
    }

    /// <summary>
    /// Whether <paramref name="location"/> is <paramref name="path"/> or one of the pages beneath it -
    /// "/notes/{id}/edit" is under "/notes/{id}", "/notes/{id}2" is not. The query is not part of it.
    /// </summary>
    public static bool IsUnder(string location, string path)
    {
        var locationPath = PathOf(location);
        return locationPath == path || locationPath.StartsWith(path + "/", StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether stepping back onto <paramref name="entry"/> takes the reader to
    /// <paramref name="destination"/>. The same address does, and so does the same page with more on its
    /// address than the destination names: a screen that says "back to /calendar" means the calendar the
    /// reader left, and stepping back restores the day they were looking at rather than resetting it.
    /// </summary>
    private static bool Reaches(string entry, string destination)
        => entry == destination
            || (destination.IndexOfAny(['?', '#']) < 0 && PathOf(entry) == destination);

    private static string PathOf(string location)
        => location.IndexOfAny(['?', '#']) is var end and >= 0 ? location[..end] : location;

    /// <param name="StepsBack">How many entries back the browser was sent - none for a replacement.</param>
    /// <param name="Location">The address it should arrive at.</param>
    /// <param name="ThenReplaceWith">What to replace that entry with once it has arrived, if anything.</param>
    private sealed record Expectation(int StepsBack, string Location, string? ThenReplaceWith);
}

/// <summary>
/// One way of leaving a page - see <see cref="InAppHistory.Leave"/>. Go back <paramref name="StepsBack"/>
/// entries, then replace the entry arrived at with <paramref name="ReplaceWith"/> when there is one. None
/// back and something to replace with is an ordinary replacing navigation.
/// </summary>
public sealed record FinishStep(int StepsBack, string? ReplaceWith);
