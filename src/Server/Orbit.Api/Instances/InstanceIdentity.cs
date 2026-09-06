namespace Orbit.Api.Instances;

/// <summary>
/// Who this process is, for as long as it runs.
///
/// It exists so an instance can recognise its own notices coming back to it. NOTIFY is delivered to
/// every listener on a channel, the sender included, and a sender has by definition already done
/// locally whatever it is telling the others to do.
///
/// A fresh Guid per process rather than a host name or a replica id: it has to be unique among the
/// instances running right now, and nothing else. Container Apps replicas come and go without asking.
/// </summary>
public sealed class InstanceIdentity
{
    public Guid Id { get; } = Guid.NewGuid();
}
