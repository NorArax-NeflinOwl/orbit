using Orbit.Mobile.Screens.Dashboard;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>The dashboard's layout for one reader - what is put away and what each card is filtered to - kept for as long as one test runs.</summary>
internal sealed class InMemoryDashboardCardPreferenceStore : IDashboardCardPreferenceStore
{
    private HashSet<DashboardCardKind> _hidden = [];
    private Dictionary<DashboardCardKind, DashboardCardFilter> _filters = [];

    public IReadOnlySet<DashboardCardKind> ReadHidden() => _hidden;

    public void WriteHidden(IReadOnlySet<DashboardCardKind> hidden) => _hidden = [.. hidden];

    public IReadOnlyDictionary<DashboardCardKind, DashboardCardFilter> ReadFilters() => _filters;

    public void WriteFilters(IReadOnlyDictionary<DashboardCardKind, DashboardCardFilter> filters)
        => _filters = filters.ToDictionary(filter => filter.Key, filter => filter.Value);
}
