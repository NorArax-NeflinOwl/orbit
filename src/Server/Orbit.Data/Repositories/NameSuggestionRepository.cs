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
            // Distinct after ordering rather than before: the same product in two warehouses is one
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
    /// from somebody else's data would be telling them what that person keeps in their warehouse.
    /// </summary>
    private IQueryable<string> NamesFor(Guid userId, NameSuggestionKind kind)
        => kind switch
        {
            NameSuggestionKind.InventoryItemName => _dbContext.InventoryItems
                .AsNoTracking()
                .Where(item => _dbContext.Warehouses
                    .Any(warehouse => warehouse.Id == item.WarehouseId && warehouse.UserId == userId && !warehouse.IsPrivate))
                .Select(item => item.Name),

            NameSuggestionKind.WarehouseName => _dbContext.Warehouses
                .AsNoTracking()
                .Where(warehouse => warehouse.UserId == userId && !warehouse.IsPrivate)
                .Select(warehouse => warehouse.Name),

            NameSuggestionKind.TaskListTitle => _dbContext.Tasks
                .AsNoTracking()
                .Where(task => task.UserId == userId && !task.IsPrivate)
                .Select(task => task.Title),

            NameSuggestionKind.TaskItemDescription => _dbContext.Tasks
                .AsNoTracking()
                .Where(task => task.UserId == userId && !task.IsPrivate)
                .SelectMany(task => task.Items)
                .Select(item => item.Description),

            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No names are kept for that kind.")
        };
}
