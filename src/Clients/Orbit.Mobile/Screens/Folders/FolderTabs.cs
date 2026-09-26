using Orbit.Core.Folders;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Folders;

/// <summary>
/// One folder as a list screen offers it: what it is called, how many things are in it, and whether it
/// is the one being read.
/// </summary>
/// <param name="Count">
/// How many of the screen's own rows are in it. Counted from where each row actually is rather than
/// stored anywhere - see <see cref="FolderPlacement"/>, which is what decides that, and which is why a
/// folder cannot disagree with what is in it.
/// </param>
/// <param name="HasNews">
/// Whether anything under it is something the reader has not seen - see <see cref="FolderTabs.Describe"/>,
/// and the dot the browser puts on the tab this entry stands for.
/// </param>
public sealed record FolderChoice(FolderKey Key, string Name, int Count, bool IsChosen, bool HasNews = false);

/// <summary>
/// Where one of a screen's rows is, and whether anything unread is about it - what
/// <see cref="FolderTabs.Describe"/> counts and marks the folders from.
/// </summary>
public readonly record struct RowInAFolder(FolderKey Where, bool HasNews = false);

/// <summary>
/// The folders one list screen offers, and which of them is being read. The browser draws these as a
/// row of tabs above the cards; the phone has no room for a row of tabs, so they are entries in the
/// menu under the screen's name - which is what the design draws, counts and all.
///
/// One class for the two screens that have folders rather than one each, for the same reason
/// <see cref="ListArrangement"/> is one type: the rules are the same on both, and a rule spelled out on
/// the notes and forgotten on the tasks is how a Finished tab ends up on a screen where nothing can be
/// finished. What differs is the <see cref="FolderPage"/> it is given, and everything that follows from
/// that is asked of <see cref="FolderPages"/>.
/// </summary>
public sealed class FolderTabs
{
    private readonly LocalFolderRepository _folders;
    private readonly IChosenFolderStore _chosen;
    private readonly Translations _translations;
    private IReadOnlyList<LocalFolder> _made = [];

    public FolderTabs(
        LocalFolderRepository folders, IChosenFolderStore chosen, Translations translations, FolderPage page)
    {
        _folders = folders;
        _chosen = chosen;
        _translations = translations;
        Page = page;
        Chosen = chosen.Read(page);
    }

    public FolderPage Page { get; }

    /// <summary>Which folder the screen is being read under. Public until somebody says otherwise.</summary>
    public FolderKey Chosen { get; private set; }

    /// <summary>The folders somebody made on this page, in the order they read - for filing into.</summary>
    public IReadOnlyList<LocalFolder> Made => _made;

    public bool HasAnyMade => _made.Count > 0;

    /// <summary>Reads the tabs this screen has. Cheap: a handful of rows with a name each.</summary>
    public async Task ReadAsync(CancellationToken cancellationToken = default)
    {
        var scopes = Page.ScopesOn().Select(scope => scope.ToString()).ToHashSet();
        _made = [.. (await _folders.GetAllAsync(cancellationToken)).Where(folder => scopes.Contains(folder.Scope))];

        // Only the dashboard leaves a hidden folder out: the page the folder belongs to keeps it, with
        // everything in it, and "hide on the dashboard" means exactly that much. What was filed under
        // it is then placed as though the folder were not there - back under a built-in one, which is
        // what FolderPlacement does with an id it is not given - so nothing disappears from the screen.
        if (Page == FolderPage.Dashboard)
        {
            var hidden = _chosen.ReadHiddenOnTheDashboard();
            _made = [.. _made.Where(folder => !hidden.Contains(folder.LocalId))];
        }

        // A folder chosen and then deleted - here or in the browser - would leave the screen narrowed to
        // a tab that no longer exists, which reads as everything having been lost. A folder hidden while
        // it was open falls back the same way, so a page can never be filtered to a folder nobody can see.
        if (Chosen.FolderId is { } id && _made.All(folder => folder.LocalId != id))
        {
            Choose(FolderKey.Default);
        }
    }

    /// <summary>Whether this folder is kept off the dashboard's menu - see <see cref="HideOnTheDashboard"/>.</summary>
    public bool IsHiddenOnTheDashboard(Guid folderId) => _chosen.ReadHiddenOnTheDashboard().Contains(folderId);

    /// <summary>
    /// Takes a folder's entry off the dashboard, or puts it back. The dashboard borrows both pages'
    /// folders, which is how one for recipes ends up between Public and Private on the screen somebody
    /// opens to see what is on their plate; this hides it there and nothing else. Kept on the device,
    /// the way the browser keeps its own (`DashboardCardPreferences.IsFolderShown`).
    /// </summary>
    public void HideOnTheDashboard(Guid folderId, bool hidden)
    {
        var current = _chosen.ReadHiddenOnTheDashboard().ToHashSet();
        if (hidden ? current.Add(folderId) : current.Remove(folderId))
        {
            _chosen.WriteHiddenOnTheDashboard(current);
        }
    }

    /// <summary>
    /// Where one row sits, by the one rule both clients share. <paramref name="isFinished"/> is passed
    /// false by a screen with no Finished tab, which is how a finished list stays visible there instead
    /// of being filed under a tab that screen does not draw - see <see cref="FolderPages.HasAFinishedTab"/>.
    /// </summary>
    /// <param name="isArchived">
    /// Whether its owner has put it away, which beats every other answer - see FolderPlacement. Not
    /// gated on the screen: every screen draws the Archived tab, because something put away has to be
    /// somewhere it can be found again.
    /// </param>
    public FolderKey Where(Guid? folderId, bool isPrivate, bool isFinished, bool isArchived = false)
        => FolderPlacement.Of(
            folderId, isPrivate, isFinished && Page.HasAFinishedTab(), [.. _made.Select(folder => folder.LocalId)],
            isArchived);

    /// <summary>
    /// Whether a row in <paramref name="where"/> belongs on the screen as it is being read. Not an
    /// equality since 2026-09-24: the widest folder is <b>All</b> now and holds what is filed in every
    /// folder, not only what is filed nowhere - see <see cref="FolderKey.Holds"/>, the one rule both
    /// clients read, so a note cannot be under a menu entry here and off the same tab in the browser.
    /// </summary>
    public bool Holds(FolderKey where) => Chosen.Holds(where);

    /// <summary>
    /// Narrows another screen to the folder this one is being read under, before somebody is taken
    /// there. What a card on the dashboard is for: it is a way into the section behind it, and arriving
    /// at that section under whatever folder it happened to be left on last is a press that answers a
    /// different question from the one asked. Reported on 2026-09-24 - "going from folder A through a
    /// card opens the list of what is in folder B".
    ///
    /// Matched by <b>name</b>, not by id: a folder holds one kind of thing, so "Home" on the notes and
    /// "Home" on the task lists are two stored folders, and the dashboard draws both kinds side by
    /// side - which is why the browser reads them as one tab there (FolderTabRow.On). A name with no
    /// folder of its own on that screen, and a built-in tab that screen does not draw, both fall back
    /// to Public rather than narrowing it to something it cannot show.
    /// </summary>
    public async Task ChooseTheSameOnAsync(FolderPage page, CancellationToken cancellationToken = default)
    {
        if (page == Page)
        {
            return;
        }

        _chosen.Write(page, await TheSameOnAsync(page, cancellationToken));
    }

    private async Task<FolderKey> TheSameOnAsync(FolderPage page, CancellationToken cancellationToken)
    {
        if (Chosen.FolderId is not { } folderId)
        {
            // A built-in one is the same word on both screens - unless that screen has no such tab,
            // and then there is nothing to carry. See FolderPages.
            return Chosen.BuiltIn switch
            {
                BuiltInFolder.Private when !page.HasAPrivateTab() => FolderKey.Default,
                BuiltInFolder.Finished when !page.HasAFinishedTab() => FolderKey.Default,
                BuiltInFolder.Archived when !page.HasAnArchivedTab() => FolderKey.Default,
                _ => Chosen
            };
        }

        var all = await _folders.GetAllAsync(cancellationToken);
        if (all.FirstOrDefault(folder => folder.LocalId == folderId) is not { } here)
        {
            return FolderKey.Default;
        }

        var scopes = page.ScopesOn().Select(scope => scope.ToString()).ToHashSet();
        var there = all.FirstOrDefault(folder =>
            scopes.Contains(folder.Scope)
            && string.Equals(folder.Name.Trim(), here.Name.Trim(), StringComparison.CurrentCultureIgnoreCase));

        return there is null ? FolderKey.Default : FolderKey.Of(there.LocalId);
    }

    /// <summary>
    /// Which folders the screen's rows are filed under, told by the screen as it reads them - the id
    /// each row carries, not the tab it ends up on.
    ///
    /// A different question from the counts <see cref="Describe(IReadOnlyList{RowInAFolder})"/> works
    /// out, and this is why it is asked separately: those are about the tab a row is drawn under, and
    /// something put away is drawn under Archived wherever it is filed - so a folder holding nothing
    /// but archived things counts zero and still holds them.
    ///
    /// Asked before a folder can be deleted. A screen that never says anything is taken at its word
    /// that its folders are empty, which is what every screen did before the rule existed - see
    /// Orbit.Web's FolderTabs.StillHolds, where it was decided on 2026-09-20.
    /// </summary>
    public void NoteWhatIsFiled(IEnumerable<Guid?> folderIds)
        => _filed = folderIds.OfType<Guid>().ToHashSet();

    /// <summary>Where the screen's rows are filed - see <see cref="NoteWhatIsFiled"/>.</summary>
    private IReadOnlySet<Guid> _filed = new HashSet<Guid>();

    /// <summary>Whether anything at all is still filed under this folder.</summary>
    public bool StillHolds(Guid folderId) => _filed.Contains(folderId);

    /// <summary>
    /// Whether the folder being read still holds something, so the menu can grey "Delete folder"
    /// rather than offer a press that empties a folder into Public under a word that promised to
    /// remove one. False while a built-in folder is open: those cannot be deleted at all.
    /// </summary>
    public bool ChosenStillHolds => Chosen.FolderId is { } folderId && StillHolds(folderId);

    /// <summary>
    /// Which kind of thing the folder being read holds - null while a built-in one is open, since those
    /// are not about one kind, and null too for an id this screen has no folder for.
    ///
    /// Only the dashboard asks: it draws both pages' tabs, so opening one somebody made is a question
    /// about one kind of card and leaves every other card answering something nobody asked.
    /// </summary>
    public FolderScope? ChosenScope
        => Chosen.FolderId is { } id
            && _made.FirstOrDefault(folder => folder.LocalId == id) is { } folder
            && Enum.TryParse<FolderScope>(folder.Scope, out var scope)
                ? scope
                : null;

    /// <summary>Written down as it is chosen, the way every other setting on this phone is.</summary>
    public void Choose(FolderKey key)
    {
        Chosen = key;
        _chosen.Write(Page, key);
    }

    /// <summary>
    /// The entries the menu draws: the built-in folders this page has, then the ones somebody made,
    /// each with how many of the screen's rows are in it.
    ///
    /// Everything is offered whether or not it holds anything. A tab that appeared only once something
    /// was in it could never be filed into in the first place, and an empty one showing "0" is how a
    /// reader finds out where the thing they filed did *not* go.
    /// </summary>
    public IReadOnlyList<FolderChoice> Describe(IEnumerable<FolderKey> whereEachRowIs)
        => Describe([.. whereEachRowIs.Select(where => new RowInAFolder(where))]);

    /// <inheritdoc cref="Describe(IEnumerable{FolderKey})"/>
    /// <remarks>
    /// The overload that also marks: a folder holding something the reader has not seen says so, the way
    /// the browser's tab does. A screen that does not read the feed uses the one above and marks nothing.
    /// </remarks>
    public IReadOnlyList<FolderChoice> Describe(IReadOnlyList<RowInAFolder> rows)
    {
        var counts = rows.GroupBy(row => row.Where).ToDictionary(group => group.Key, group => group.Count());
        _news = rows.Where(row => row.HasNews).Select(row => row.Where).ToHashSet();

        // "All" since 2026-09-24, and the entry is wider than its old name as well as differently named:
        // it gathers what is filed in every folder - see BuiltInFolder.All and FolderKey.Holds.
        List<FolderChoice> choices = [Choice(FolderKey.Of(BuiltInFolder.All), _translations["All"], counts)];

        // Gated the way the browser's row gates it, which this did not do: an event cannot be sealed at
        // all, so the calendar's menu offered a tab that could only ever read zero - see
        // FolderPages.HasAPrivateTab, which was written for the browser and not asked here.
        if (Page.HasAPrivateTab())
        {
            choices.Add(Choice(FolderKey.Of(BuiltInFolder.Private), _translations["Private"], counts));
        }

        if (Page.HasAFinishedTab())
        {
            choices.Add(Choice(FolderKey.Of(BuiltInFolder.Finished), _translations["Finished"], counts));
        }

        // Last of the built-in ones, and on every screen but the dashboard - see
        // FolderPages.HasAnArchivedTab, which says why that one is the exception. Gated here as well as
        // in the browser's row for the reason the Private tab above is: one rule, read by both.
        if (Page.HasAnArchivedTab())
        {
            choices.Add(Choice(FolderKey.Of(BuiltInFolder.Archived), _translations["Archived"], counts));
        }

        choices.AddRange(_made.Select(folder => Choice(FolderKey.Of(folder.LocalId), folder.Name, counts)));
        return choices;
    }

    /// <summary>
    /// Which folders hold something unread, as the last <see cref="Describe(IReadOnlyList{RowInAFolder})"/>
    /// worked it out. Held rather than passed down, because Choice is called once per folder and the
    /// answer is about all of them at once.
    /// </summary>
    private IReadOnlySet<FolderKey> _news = new HashSet<FolderKey>();

    /// <summary>
    /// One entry, with how many rows are under it. Summed over every folder the entry holds rather than
    /// looked up by its own key: All holds them all bar the sealed and the put-away, so a count of what
    /// is filed nowhere would say a smaller number than the screen goes on to draw. See FolderKey.Holds.
    /// </summary>
    private FolderChoice Choice(FolderKey key, string name, IReadOnlyDictionary<FolderKey, int> counts)
        => new(
            key, name, counts.Where(under => key.Holds(under.Key)).Sum(under => under.Value), key == Chosen,
            _news.Any(key.Holds));
}

/// <summary>
/// Which folder each list screen is being read under, kept on the device - beside how it is sorted and
/// what it is narrowed to, and for the same reason: it is how one person reads one screen on one phone
/// and says nothing about the notes or the lists themselves. See <see cref="IListArrangementStore"/>.
/// </summary>
public interface IChosenFolderStore
{
    FolderKey Read(FolderPage page);

    void Write(FolderPage page, FolderKey chosen);

    /// <summary>
    /// The folders somebody made that are kept off the dashboard's menu - see
    /// <see cref="FolderTabs.HideOnTheDashboard"/>. Here rather than in a store of its own because it
    /// is the same kind of answer: how one person reads one screen on one phone, and nothing about the
    /// folders themselves. The browser keeps its own beside its put-away cards, for the same reason.
    /// </summary>
    IReadOnlySet<Guid> ReadHiddenOnTheDashboard();

    void WriteHiddenOnTheDashboard(IReadOnlySet<Guid> hidden);
}
