using Orbit.Contracts.Tasks;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// What the reader is looking for among the entries on every list: a word from the entry itself, and
/// the categories it is filed under. One object rather than three loose fields on the screen, because
/// they are asked together, cleared together and answered together. The same filter Orbit.Web's tasks
/// page applies - see its TaskItemFilter.
///
/// The two narrow independently - a search and a category both set means both must hold. Among the
/// categories themselves the reader chooses: any of them by default, because that is what picking a
/// second one usually means ("show me the shopping and the car"), or all of them where they mean an
/// entry that is both at once.
/// </summary>
public sealed class TaskItemFilter
{
    private readonly List<string> _categories = [];

    /// <summary>A word from the entry's own words. Matched anywhere in them, not just at the start.</summary>
    public string Search { get; set; } = string.Empty;

    /// <summary>The categories chosen, in the order they were pressed.</summary>
    public IReadOnlyList<string> Categories => _categories;

    /// <summary>
    /// Whether an entry has to carry every chosen category rather than any one of them. Off by
    /// default - see the class comment.
    /// </summary>
    public bool MatchesEveryCategory { get; set; }

    /// <summary>Whether anything has actually been asked for. Everything is shown until something has.</summary>
    public bool IsActive => Search.Trim().Length > 0 || _categories.Count > 0;

    public bool IsChosen(string category)
        => _categories.Contains(category, StringComparer.CurrentCultureIgnoreCase);

    public void Toggle(string category)
    {
        if (_categories.RemoveAll(chosen => string.Equals(chosen, category, StringComparison.CurrentCultureIgnoreCase)) == 0)
        {
            _categories.Add(category);
        }
    }

    public void Clear()
    {
        Search = string.Empty;
        _categories.Clear();
        MatchesEveryCategory = false;
    }

    public bool Matches(TaskItemDto item) => MatchesSearch(item) && MatchesCategories(item);

    /// <summary>Whether a list has anything worth showing under this filter - one matching entry is enough.</summary>
    public bool HasAMatch(LocalTaskList taskList) => !IsActive || taskList.Items.Any(Matches);

    /// <summary>
    /// The first entry that answered, for a card to show in place of what it would otherwise say: a list
    /// left on screen for a match nobody can see reads as a bug.
    /// </summary>
    public TaskItemDto? FirstMatch(LocalTaskList taskList)
        => IsActive ? taskList.Items.FirstOrDefault(Matches) : null;

    private bool MatchesSearch(TaskItemDto item)
    {
        var wanted = Search.Trim();
        return wanted.Length == 0
            || item.Description.Contains(wanted, StringComparison.CurrentCultureIgnoreCase);
    }

    private bool MatchesCategories(TaskItemDto item)
    {
        if (_categories.Count == 0)
        {
            return true;
        }

        var carried = item.AllCategories;
        return MatchesEveryCategory
            ? _categories.All(chosen => carried.Contains(chosen, StringComparer.CurrentCultureIgnoreCase))
            : _categories.Any(chosen => carried.Contains(chosen, StringComparer.CurrentCultureIgnoreCase));
    }
}

/// <summary>One category the reader can narrow by, and how many entries it would leave.</summary>
public sealed record TaskCategoryChoice(string Name, int Count, bool IsChosen);
