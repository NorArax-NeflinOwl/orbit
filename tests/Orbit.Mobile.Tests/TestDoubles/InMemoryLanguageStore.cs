using Orbit.Localization;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>Stands in for the phone's preferences, so a restart can be simulated over the same store.</summary>
internal sealed class InMemoryLanguageStore : ILanguageStore
{
    private AppLanguage _language = AppLanguage.English;

    public AppLanguage Read() => _language;

    public void Write(AppLanguage language) => _language = language;
}
