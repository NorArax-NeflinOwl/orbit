namespace Orbit.Api.LiveUpdates;

/// <summary>
/// Who this process is, for as long as it runs.
///
/// It exists so an instance can recognise its own announcements coming back to it. NOTIFY is delivered
/// to every listener on the channel, the sender included, and the sender has already delivered locally
/// by the time it sends - so without something to compare against, every client connected to the
/// instance that did the work would be told twice.
///
/// A fresh Guid per process rather than a host name or a replica id: it has to be unique among the
/// instances running right now, and nothing else. Container Apps replicas come and go without asking.
/// </summary>
public sealed class LiveUpdateInstance
{
    public Guid Id { get; } = Guid.NewGuid();
}
