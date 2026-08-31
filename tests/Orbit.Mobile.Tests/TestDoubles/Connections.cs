using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// A <see cref="ConnectionRequirement"/> for a test that does not care about one. Most screens now take
/// it so they can grey out what needs the server; only the tests that are about that greying need to
/// build their own from a network they can change.
/// </summary>
internal static class Connections
{
    public static ConnectionRequirement Online => For(FixedNetworkStatus.Online);

    public static ConnectionRequirement Offline => For(FixedNetworkStatus.Offline);

    public static ConnectionRequirement For(INetworkStatus networkStatus)
        => new(networkStatus, new Translations(new InMemoryLanguageStore()));
}
