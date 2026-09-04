using Microsoft.EntityFrameworkCore;
using Orbit.Core.Suggestions;

namespace Orbit.Data.Repositories;

/// <summary>
/// The words this reader has already filed things under, per field - see <see cref="UsedValueKind"/>.
///
/// Plain DISTINCT rather than the trigram search next door in <see cref="NameSuggestionRepository"/>,
/// and that is the point: a category list is short, it is shown before anything is typed, and the whole
/// of it is what somebody wants to see. It works on SQLite as well as on Postgres, so the tests that
/// use one exercise this path rather than skipping it.
/// </summary>
public sealed class UsedValueRepository(OrbitDbContext dbContext) : IUsedValueRepository
{
    public async Task<IReadOnlyList<string>> FindAllAsync(
        Guid userId, UsedValueKind kind, CancellationToken cancellationToken)
    {
        var used = await ValuesFor(userId, kind)
            .Where(value => value != string.Empty)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Folded together here rather than in SQL: "Food" and "food" are one category to somebody
        // filing things, and which of the two spellings the database happens to return first is not a
        // decision worth taking on the reader's behalf - the earliest alphabetically is at least stable.
        return [.. used
            .GroupBy(value => value, StringComparer.CurrentCultureIgnoreCase)
            .Select(spellings => spellings.OrderBy(spelling => spelling, StringComparer.CurrentCulture).First())
            .OrderBy(value => value, StringComparer.CurrentCulture)];
    }

    private IQueryable<string> ValuesFor(Guid userId, UsedValueKind kind)
        => kind switch
        {
            UsedValueKind.TaskItemCategory => dbContext.Tasks
                .AsNoTracking()
                .Where(task => task.UserId == userId && !task.IsPrivate)
                .SelectMany(task => task.Items)
                .SelectMany(item => item.Categories)
                .Select(category => category.Category),

            UsedValueKind.InventoryItemCategory => OwnItems(userId)
                .SelectMany(item => item.Categories)
                .Select(category => category.Category),

            UsedValueKind.InventoryItemProductType => OwnItems(userId).Select(item => item.ProductType),

            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Nothing is filed under that.")
        };

    /// <summary>
    /// The shelf items on this reader's own inventories. A private one keeps no item rows at all - see
    /// UpdateInventoryCommandHandler - so the IsPrivate filter is belt and braces rather than the thing
    /// doing the work.
    /// </summary>
    private IQueryable<Entities.InventoryItemEntity> OwnItems(Guid userId)
        => dbContext.InventoryItems
            .AsNoTracking()
            .Where(item => dbContext.Inventories
                .Any(inventory => inventory.Id == item.InventoryId && inventory.UserId == userId && !inventory.IsPrivate));
}
