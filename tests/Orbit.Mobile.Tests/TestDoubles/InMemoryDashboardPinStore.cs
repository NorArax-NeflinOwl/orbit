using Orbit.Mobile.Screens.Dashboard;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>The pinned dashboard cards, kept for as long as one test runs.</summary>
internal sealed class InMemoryDashboardPinStore : IDashboardPinStore
{
    private HashSet<DashboardCardKind> _pinned = [];

    public IReadOnlySet<DashboardCardKind> Read() => _pinned;

    public void Write(IReadOnlySet<DashboardCardKind> pinned) => _pinned = [.. pinned];
}
