using Orbit.Contracts.Tasks;
using Orbit.Core.Tasks.TagFilters;
using Orbit.Mobile.Api;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// Where this phone keeps the account's Tasks card filters as last read, and which one the card shows.
/// An interface for the reason every device preference here has one: the screens reading it are view
/// models this project tests.
/// </summary>
public interface ITaskTagFilterStore
{
    IReadOnlyList<TaskTagFilterDto> ReadFilters();

    void WriteFilters(IReadOnlyList<TaskTagFilterDto> filters);

    /// <summary>The filter the dashboard's Tasks card is showing, or null for none.</summary>
    Guid? ReadChosen();

    void WriteChosen(Guid? filterId);
}

/// <summary>
/// The account's filters for the dashboard's Tasks card, on the phone - see
/// Orbit.Core.Tasks.TagFilters.TaskTagFilter, and Orbit.Web's TagFilterDialog.
///
/// Kept as a copy on the device and read again whenever the dashboard synchronises, rather than stored in
/// the local database and queued like a note: a filter is a setting made now and then, not something
/// written on a train, so making or deleting one asks for a connection and says so when there is none -
/// the way sharing does. Reading them works offline from the copy.
/// </summary>
public sealed class TaskTagFilters(TasksClient client, ITaskTagFilterStore store)
{
    public IReadOnlyList<TaskTagFilterDto> Held => store.ReadFilters();

    /// <summary>The filter the Tasks card shows, when it is one this account still has.</summary>
    public TaskTagFilterDto? Chosen => store.ReadChosen() is { } chosen ? Held.FirstOrDefault(filter => filter.Id == chosen) : null;

    public void Choose(Guid? filterId) => store.WriteChosen(filterId);

    /// <summary>
    /// Reads them again from the server. Answers whether anything changed, so a screen redraws only when
    /// it has to; offline, the copy stands and nothing changed.
    /// </summary>
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var fresh = await client.GetTagFiltersAsync(cancellationToken);
            var before = Held;
            store.WriteFilters(fresh);
            return !fresh.Select(Key).SequenceEqual(before.Select(Key));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Makes one and keeps it - or answers null when it could not be made.</summary>
    public async Task<TaskTagFilterDto?> CreateAsync(IReadOnlyList<string> tags, bool matchesAll, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await client.CreateTagFilterAsync(tags, matchesAll, cancellationToken) is not { } created)
            {
                return null;
            }

            store.WriteFilters([.. Held, created]);
            return created;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>Takes one away, and stops the card showing it. False when there was no connection to do it with.</summary>
    public async Task<bool> DeleteAsync(Guid filterId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await client.DeleteTagFilterAsync(filterId, cancellationToken))
            {
                return false;
            }
        }
        catch (HttpRequestException)
        {
            return false;
        }

        store.WriteFilters([.. Held.Where(filter => filter.Id != filterId)]);
        if (store.ReadChosen() == filterId)
        {
            store.WriteChosen(null);
        }

        return true;
    }

    /// <summary>Whether a list carrying these tags is one the filter finds - the rule Orbit.Core keeps.</summary>
    public static bool Finds(TaskTagFilterDto filter, IEnumerable<string> listTags) => AsFilter(filter).Matches(listTags);

    /// <summary>What a filter is called on a menu: its tags, joined by the reader's "or" or "and".</summary>
    public static string NameOf(TaskTagFilterDto filter, Translations translations)
        => AsFilter(filter).Describe(translations["or"], translations["and"]);

    private static TaskTagFilter AsFilter(TaskTagFilterDto filter)
        => new(filter.Id, filter.Tags, filter.MatchesAll, filter.CreatedAtUtc);

    private static string Key(TaskTagFilterDto filter)
        => $"{filter.Id}|{filter.MatchesAll}|{string.Join("|", filter.Tags)}";
}
