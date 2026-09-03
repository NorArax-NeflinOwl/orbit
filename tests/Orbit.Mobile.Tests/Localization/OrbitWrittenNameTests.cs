using Orbit.Core.Inventories;
using Orbit.Localization;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Localization;

/// <summary>
/// The names Orbit writes for itself, on the phone. The browser has read them in the reader's language
/// for a while; the phone showed them exactly as the server stored them, so an otherwise Polish Tasks
/// screen carried a list called "Restock supplies - Kuchnia" and a row saying "Restock: Mąka (5 kg)".
///
/// What is stored still stays English - the server recognises its own list again by that name - so these
/// check the translation happens on the way to the screen and only there.
/// </summary>
public sealed class OrbitWrittenNameTests
{
    [Fact]
    public void A_restock_list_Orbit_named_is_read_in_the_readers_language()
    {
        var translations = InPolish();

        var written = translations.Written($"{RestockTaskNaming.ListTitlePrefix}Kuchnia");

        Assert.DoesNotContain("Restock supplies", written);
        Assert.EndsWith("Kuchnia", written);
    }

    /// <summary>The part a person chose rides along untouched, which is the whole trick.</summary>
    [Fact]
    public void A_name_somebody_typed_comes_back_exactly_as_they_typed_it()
    {
        Assert.Equal("Zakupy na weekend", InPolish().Written("Zakupy na weekend"));
    }

    [Fact]
    public void An_errand_Orbit_wrote_is_read_in_the_readers_language_too()
    {
        var written = InPolish().Written($"{RestockTaskNaming.EntryPrefix}Mąka (5 kg)");

        Assert.DoesNotContain("Restock:", written);
        Assert.Contains("Mąka", written);
    }

    private static Translations InPolish()
    {
        var translations = new Translations(new InMemoryLanguageStore());
        translations.SetLanguage(AppLanguage.Polish);
        return translations;
    }
}
