using Orbit.Mobile.Presence;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>Stands in for the phone's preferences, so a restart can be simulated by building a second Presence over the same store.</summary>
internal sealed class InMemoryPresenceStore : IPresenceStore
{
    private ChosenAvailability _chosen = ChosenAvailability.Available;

    public ChosenAvailability Read() => _chosen;

    public void Write(ChosenAvailability availability) => _chosen = availability;
}
