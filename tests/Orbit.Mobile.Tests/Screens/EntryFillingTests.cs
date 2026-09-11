using Orbit.Mobile.Screens.Suggestions;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Which kinds of entry a picked name fills in on this phone - see EntryFilling and the account screen's
/// Preferences tab. The user's rule: every kind until somebody says otherwise.
/// </summary>
public sealed class EntryFillingTests
{
    private sealed class InMemoryEntryFillingStore : IEntryFillingStore
    {
        private IReadOnlySet<string>? _kinds;

        public IReadOnlySet<string>? Read() => _kinds;

        public void Write(IReadOnlySet<string> kinds) => _kinds = kinds;
    }

    [Fact]
    public void A_phone_never_asked_fills_in_every_kind()
    {
        var filling = new EntryFilling(new InMemoryEntryFillingStore());

        Assert.All(EntryFilling.EntryKinds, kind => Assert.True(filling.Fills(kind)));
    }

    [Fact]
    public void A_kind_switched_off_stays_off_and_the_others_stay_on()
    {
        var filling = new EntryFilling(new InMemoryEntryFillingStore());

        filling.SetFills("Calendar", fills: false);

        Assert.False(filling.Fills("Calendar"));
        Assert.True(filling.Fills("Inventory"));
    }
}
