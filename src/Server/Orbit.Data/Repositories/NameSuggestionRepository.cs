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
