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
public sealed record FolderChoice(FolderKey Key, string Name, int Count, bool IsChosen);

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
    public FolderKey Where(Guid? folderId, bool isPrivate, bool isFinished)
        => FolderPlacement.Of(
            folderId, isPrivate, isFinished && Page.HasAFinishedTab(), [.. _made.Select(folder => folder.LocalId)]);

    /// <summary>Whether a row in <paramref name="where"/> belongs on the screen as it is being read.</summary>
    public bool Holds(FolderKey where) => where == Chosen;

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
    {
        var counts = whereEachRowIs.GroupBy(where => where).ToDictionary(rows => rows.Key, rows => rows.Count());

        List<FolderChoice> choices =
        [
            Choice(FolderKey.Of(BuiltInFolder.Public), _translations["Public"], counts),
            Choice(FolderKey.Of(BuiltInFolder.Private), _translations["Private"], counts)
        ];

        if (Page.HasAFinishedTab())
        {
            choices.Add(Choice(FolderKey.Of(BuiltInFolder.Finished), _translations["Finished"], counts));
        }

        choices.AddRange(_made.Select(folder => Choice(FolderKey.Of(folder.LocalId), folder.Name, counts)));
        return choices;
    }

    private FolderChoice Choice(FolderKey key, string name, IReadOnlyDictionary<FolderKey, int> counts)
        => new(key, name, counts.TryGetValue(key, out var count) ? count : 0, key == Chosen);
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
