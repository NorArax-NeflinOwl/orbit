using Orbit.Api.HealthChecks;
using Xunit;

namespace Orbit.Api.Tests.HealthChecks;

public sealed class HostedServiceHealthTrackerTests
{
    [Fact]
    public void GetLastHeartbeats_returns_empty_when_nothing_has_reported()
    {
        var tracker = new HostedServiceHealthTracker();

        Assert.Empty(tracker.GetLastHeartbeats());
    }

    [Fact]
    public void ReportHeartbeat_records_one_entry_per_service_name()
    {
        var tracker = new HostedServiceHealthTracker();

        tracker.ReportHeartbeat("note-sync-worker");
        tracker.ReportHeartbeat("note-sync-worker");

        Assert.Single(tracker.GetLastHeartbeats());
        Assert.Contains("note-sync-worker", tracker.GetLastHeartbeats().Keys);
    }
}
