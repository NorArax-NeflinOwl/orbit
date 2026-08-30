using Orbit.Mobile.Api;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Suggestions;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// A <see cref="NameSuggestions"/> for an editor test that is not about suggestions. Every editor screen
/// holds one, so every editor test has to hand one over.
/// </summary>
internal static class Suggestions
{
    public static NameSuggestions Offering(FakeSuggestionsServer? server = null)
        => new(
            new SuggestionsClient((server ?? new FakeSuggestionsServer()).ToHttpClient()),
            new Translations(new InMemoryLanguageStore()));
}
