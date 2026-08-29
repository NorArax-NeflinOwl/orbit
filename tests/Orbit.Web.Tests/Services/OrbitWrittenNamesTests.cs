using Orbit.Localization;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// The names Orbit writes for itself, said in the reader's language. What the server stores stays
/// English - it is how the server knows its own list again - so this is the only place they change.
/// </summary>
public sealed class OrbitWrittenNamesTests
{
    private static Translations InPolish()
    {
        var translations = new Translations(new StubJSRuntime());
        translations.SetLanguageAsync(AppLanguage.Polish).GetAwaiter().GetResult();
        return translations;
    }

    [Fact]
    public void A_restock_list_is_named_in_Polish_without_losing_its_warehouse()
    {
        Assert.Equal(
            "Uzupełnienie zapasów - Spiżarnia",
            OrbitWrittenNames.Translate(InPolish(), "Restock supplies - Spiżarnia"));
    }

    [Fact]
    public void A_restock_errand_keeps_the_product_and_the_number()
    {
        Assert.Equal("Uzupełnij: Mąka (5)", OrbitWrittenNames.Translate(InPolish(), "Restock: Mąka (5)"));
    }


    [Fact]
    public void The_unit_an_errand_carries_is_said_in_Polish_too()
    {
        Assert.Equal("Uzupełnij: Mleko (2 opak.)", OrbitWrittenNames.Translate(InPolish(), "Restock: Mleko (2 pack)"));
    }

    [Fact]
    public void A_unit_that_reads_the_same_in_both_is_left_as_it_is()
    {
        Assert.Equal("Uzupełnij: Mąka (5 kg)", OrbitWrittenNames.Translate(InPolish(), "Restock: Mąka (5 kg)"));
    }

    [Fact]
    public void A_product_whose_own_name_ends_in_brackets_is_not_mistaken_for_a_unit()
    {
        // Nothing here is a unit Orbit writes, so the whole tail is somebody's own words.
        Assert.Equal(
            "Uzupełnij: Mąka (typ 500)",
            OrbitWrittenNames.Translate(InPolish(), "Restock: Mąka (typ 500)"));
    }
    [Fact]
    public void The_standing_reminder_is_translated_too()
    {
        Assert.Equal(
            "Zaktualizuj stany magazynowe",
            OrbitWrittenNames.Translate(InPolish(), "Update stock levels"));
    }

    [Fact]
    public void A_name_somebody_wrote_themselves_is_left_alone()
    {
        Assert.Equal("Zakupy", OrbitWrittenNames.Translate(InPolish(), "Zakupy"));
    }

    [Fact]
    public void An_English_reader_sees_what_the_server_stored()
    {
        var translations = new Translations(new StubJSRuntime());

        Assert.Equal("Restock supplies - Pantry", OrbitWrittenNames.Translate(translations, "Restock supplies - Pantry"));
    }
}
