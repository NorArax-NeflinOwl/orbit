using Microsoft.EntityFrameworkCore;
using Orbit.Core.Suggestions;

namespace Orbit.Data.Repositories;

/// <summary>
/// Name suggestions by PostgreSQL trigram similarity (the pg_trgm extension - see
/// OrbitDbContext.OnModelCreating, which declares it, and the GIN indexes the migration adds).
///
/// Postgres-only, deliberately and without a fallback: this is the one query in the application that
/// asks "how alike are these two strings", and doing it in memory would mean loading every name the
/// reader owns on every keystroke. The tests that use SQLite do not exercise this path.
///
/// Private items are left out throughout. Their names are sealed in the owner's browser and the column
/// here holds nothing readable, so suggesting from them would offer ciphertext.
/// </summary>
public sealed class NameSuggestionRepository : INameSuggestionRepository
{
    private readonly OrbitDbContext _dbContext;

    public NameSuggestionRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<NameSuggestion>> FindAsync(
        Guid userId, NameSuggestionKind kind, string typed, double minimumSimilarity, int limit,
        CancellationToken cancellationToken)
    {
        var names = NamesFor(userId, kind);

        var found = await names
            .Where(name => name != string.Empty)
            .Select(name => new { Name = name, Similarity = EF.Functions.TrigramsSimilarity(name, typed) })
            .Where(candidate => candidate.Similarity >= minimumSimilarity)
            .OrderByDescending(candidate => candidate.Similarity)
            .ThenBy(candidate => candidate.Name)
            // Distinct after ordering rather than before: the same product in two inventories is one
            // suggestion, and which row it came from does not matter to somebody typing.
            .Take(limit * 4)
            .ToListAsync(cancellationToken);

        return [.. found
            .GroupBy(candidate => candidate.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(byName => new NameSuggestion(byName.Key, byName.Max(candidate => candidate.Similarity)))
            .OrderByDescending(suggestion => suggestion.Similarity)
            .ThenBy(suggestion => suggestion.Name)
            .Take(limit)];
    }

    /// <summary>
    /// A plain lookup by name rather than a second similarity search: the names are already chosen, and
    /// this only asks what each is the name of. Lower-cased on both sides, which both Postgres and the
    /// SQLite the tests use translate, so this path is covered where FindAsync cannot be.
    /// </summary>
    public async Task<IReadOnlyList<NameSuggestionSource>> FindSourcesAsync(
        Guid userId, IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        if (names.Count == 0)
        {
            return [];
        }

        var lowered = names.Select(name => name.ToLower()).Distinct().ToList();

        var shelfItems = await (
                from item in _dbContext.InventoryItems.AsNoTracking()
                join inventory in _dbContext.Inventories.AsNoTracking() on item.InventoryId equals inventory.Id
                where inventory.UserId == userId && !inventory.IsPrivate && lowered.Contains(item.Name.ToLower())
                select new { item.Id, item.Name, InventoryId = inventory.Id, InventoryName = inventory.Name })
            .ToListAsync(cancellationToken);

        // A join rather than SelectMany over the list's items: that one needs SQL's APPLY, which SQLite has
        // not got, and this is the path the SQLite tests are here to cover.
        var entries = await (
                from item in _dbContext.Set<Entities.TaskItemEntity>().AsNoTracking()
                join task in _dbContext.Tasks.AsNoTracking() on item.TaskId equals task.Id
                where task.UserId == userId && !task.IsPrivate && lowered.Contains(item.Description.ToLower())
                select new
                {
                    item.Id, item.Description, item.Kind, item.ReferencesTaskItemId, item.LinkedInventoryItemId,
                    item.CreatedAtUtc, TaskListId = task.Id, TaskListTitle = task.Title
                })
            .ToListAsync(cancellationToken);

        var shelfItemIds = shelfItems.Select(item => item.Id).ToHashSet();
        var fromTheShelves = shelfItems.Select(item => new NameSuggestionSource(
            item.Name, NameSuggestionSourceKind.InventoryItem, item.Id, item.Id, item.InventoryId, item.InventoryName,
            nameof(Orbit.Core.Tasks.TaskItemKind.Inventory)));

        // One per reference group: the members all say the same thing, so offering each would be offering
        // one thing several times. Read off the source when the source is among those found, else off the
        // member stored first.
        var fromTheLists = entries
            .Where(entry => entry.LinkedInventoryItemId is not { } linked || !shelfItemIds.Contains(linked))
            .GroupBy(entry => entry.ReferencesTaskItemId ?? entry.Id)
            .Select(group => (Source: group.Key, Found: group
                .OrderBy(entry => entry.Id == group.Key ? 0 : 1)
                .ThenBy(entry => entry.CreatedAtUtc ?? DateTimeOffset.MaxValue)
                .First()))
            .Select(pick => new NameSuggestionSource(
                pick.Found.Description, NameSuggestionSourceKind.TaskItem, pick.Source, pick.Found.Id,
                pick.Found.TaskListId, pick.Found.TaskListTitle, pick.Found.Kind));

        return [.. fromTheShelves, .. fromTheLists];
    }

    /// <summary>
    /// Where each kind's names live. Every one is scoped to what this user owns - a suggestion drawn
    /// from somebody else's data would be telling them what that person keeps in their inventory.
    /// </summary>
    private IQueryable<string> NamesFor(Guid userId, NameSuggestionKind kind)
        => kind switch
        {
            NameSuggestionKind.InventoryItemName => InventoryItemNames(userId),

            NameSuggestionKind.InventoryName => _dbContext.Inventories
                .AsNoTracking()
                .Where(inventory => inventory.UserId == userId && !inventory.IsPrivate)
                .Select(inventory => inventory.Name),

            NameSuggestionKind.TaskListTitle => _dbContext.Tasks
                .AsNoTracking()
                .Where(task => task.UserId == userId && !task.IsPrivate)
                .Select(task => task.Title),

            // The one field that reads across every other kind - see NameSuggestionKind.TaskItemDescription.
            // Concat translates to UNION ALL, so each source query still runs against its own GIN
            // trigram index rather than one query scanning four tables.
            NameSuggestionKind.TaskItemDescription => TaskItemDescriptions(userId)
                .Concat(InventoryItemNames(userId))
                .Concat(NoteTitles(userId))
                .Concat(CalendarEventTitles(userId)),

            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No names are kept for that kind.")
        };

    private IQueryable<string> InventoryItemNames(Guid userId)
        => _dbContext.InventoryItems
            .AsNoTracking()
            .Where(item => _dbContext.Inventories
                .Any(inventory => inventory.Id == item.InventoryId && inventory.UserId == userId && !inventory.IsPrivate))
            .Select(item => item.Name);

    private IQueryable<string> TaskItemDescriptions(Guid userId)
        => _dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.UserId == userId && !task.IsPrivate)
            .SelectMany(task => task.Items)
            .Select(item => item.Description);

    private IQueryable<string> NoteTitles(Guid userId)
        => _dbContext.Notes
            .AsNoTracking()
            .Where(note => note.UserId == userId && !note.IsPrivate)
            .Select(note => note.Title);

    /// <summary>
    /// Calendar events carry no IsPrivate flag at all - unlike a note, an inventory or a task list,
    /// nothing about one is ever sealed client-side, so there is no equivalent filter to apply here.
    /// </summary>
    private IQueryable<string> CalendarEventTitles(Guid userId)
        => _dbContext.CalendarEvents
            .AsNoTracking()
            .Where(calendarEvent => calendarEvent.UserId == userId)
            .Select(calendarEvent => calendarEvent.Title);
}
