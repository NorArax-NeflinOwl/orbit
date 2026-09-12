using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Which kinds of entry a picked name fills in - DevicePreferences.KindsFilledFromSuggestions, set on
/// Options' Preferences tab. The user's rule: every kind, until somebody says otherwise.
/// </summary>
public sealed class DevicePreferencesKindsTests
{
    [Fact]
    public async Task A_browser_that_was_never_asked_fills_in_every_kind()
    {
        var preferences = new DevicePreferences(new StubJSRuntime());

        await preferences.InitializeAsync();

        Assert.Equal(DevicePreferences.EntryKinds.Order(), preferences.KindsFilledFromSuggestions.Order());
    }

    [Fact]
    public async Task A_kind_switched_off_stays_off_and_the_others_stay_on()
    {
        var preferences = new DevicePreferences(new StubJSRuntime());

        await preferences.SetKindFilledFromSuggestionsAsync("Calendar", isFilled: false);

        Assert.DoesNotContain("Calendar", preferences.KindsFilledFromSuggestions);
        Assert.Contains("Inventory", preferences.KindsFilledFromSuggestions);
    }
}
