namespace Orbit.Core.Tasks.StockCheck;

/// <summary>
/// What one kind of thing a task list calls for costs against a warehouse: how many the work needs, how
/// many are on the shelf, and the difference when the shelf falls short.
/// </summary>
/// <param name="Name">The entry's description, which is what is matched against a warehouse item's name.</param>
/// <param name="Available">
/// How many of them are this list's. That is everything on the shelf where this is the only list asking
/// for it, and a share of the shelf where it is not - see StockRequirementCounter.ShareOfTheShelf.
/// </param>
/// <param name="Done">
/// How many of these the work has already ticked off. Not part of whether the job can be started - that
/// is Required against Available - but it is what a shelf built from this list starts with, since a
/// ticked line is something somebody has already fetched.
/// </param>
public sealed record StockRequirement(string Name, decimal Required, decimal Available, decimal Done = 0)
{
    /// <summary>How many more are needed. Zero when the shelf covers the work.</summary>
    public decimal Missing => Required > Available ? Required - Available : 0;

    public bool IsCovered => Missing == 0;
}

/// <summary>
/// Whether a task list's work can be done out of a warehouse, item by item.
/// </summary>
/// <param name="Requirements">Every kind of thing the work calls for, in the order it was first asked for.</param>
public sealed record TaskListStockCheck(IReadOnlyList<StockRequirement> Requirements)
{
    public static readonly TaskListStockCheck Nothing = new([]);

    /// <summary>Nothing falls short - the work can be started with what is on the shelf.</summary>
    public bool IsAchievable => Requirements.All(requirement => requirement.IsCovered);

    /// <summary>What has to be made or delivered before the work can be done, most short first.</summary>
    public IReadOnlyList<StockRequirement> Shortfalls
        => [.. Requirements.Where(requirement => !requirement.IsCovered).OrderByDescending(requirement => requirement.Missing)];
}
