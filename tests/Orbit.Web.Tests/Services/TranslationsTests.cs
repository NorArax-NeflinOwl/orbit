using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Covers how text is looked up. The fallback matters most: a key with no translation has to show the
/// English it already is, which is what makes it safe to translate the app a page at a time.
/// </summary>
public sealed class TranslationsTests
{
    [Fact]
    public async Task English_is_what_an_unanswered_question_gives()
    {
        var translations = new Translations(new StubJSRuntime());

        await translations.InitializeAsync();

        Assert.Equal(AppLanguage.English, translations.Language);
    }

    [Fact]
    public void In_English_the_key_is_the_answer()
    {
        var translations = new Translations(new StubJSRuntime());

        Assert.Equal("Add note", translations["Add note"]);
    }

    [Fact]
    public async Task In_Polish_a_known_key_is_translated()
    {
        var translations = new Translations(new StubJSRuntime());

        await translations.SetLanguageAsync(AppLanguage.Polish);

        Assert.Equal("Dodaj notatkę", translations["Add note"]);
    }

    [Fact]
    public async Task In_Polish_an_unknown_key_stays_the_English_it_already_is()
    {
        var translations = new Translations(new StubJSRuntime());
        await translations.SetLanguageAsync(AppLanguage.Polish);

        // Not a blank, and not a shouty MISSING_KEY - correct English is the right thing to show while
        // a page is still being translated.
        Assert.Equal("Something nobody has translated", translations["Something nobody has translated"]);
    }

    [Fact]
    public async Task Choosing_a_language_announces_it()
    {
        var translations = new Translations(new StubJSRuntime());
        var announced = 0;
        translations.Changed += () => announced++;

        await translations.SetLanguageAsync(AppLanguage.Polish);

        // MainLayout listens for this and re-renders, which is what makes the change take effect
        // everywhere at once instead of on the next navigation.
        Assert.Equal(1, announced);
    }

    [Fact]
    public async Task The_choice_is_remembered()
    {
        var jsRuntime = new StubJSRuntime();
        await new Translations(jsRuntime).SetLanguageAsync(AppLanguage.Polish);

        var next = new Translations(jsRuntime);
        await next.InitializeAsync();

        Assert.Equal(AppLanguage.Polish, next.Language);
    }

    [Fact]
    public void No_translation_is_an_empty_string()
    {
        // An entry that translated to nothing would render as a hole on the page, which is worse than
        // the English it replaced.
        Assert.DoesNotContain(PolishTranslationsUnderTest(), pair => string.IsNullOrWhiteSpace(pair.Value));
    }

    [Fact]
    public void No_translation_is_left_identical_to_its_English()
    {
        // A few are deliberately the same word in both languages - anything else is an entry someone
        // added and forgot to translate.
        var deliberatelyIdentical = new[] { "Min", "Debugger", "Release", "Debug", "Import", "Export" };

        var untranslated = PolishTranslationsUnderTest()
            .Where(pair => pair.Key == pair.Value && !deliberatelyIdentical.Contains(pair.Key))
            .Select(pair => pair.Key)
            .ToList();

        Assert.Empty(untranslated);
    }

    private static IReadOnlyList<KeyValuePair<string, string>> PolishTranslationsUnderTest()
    {
        // Read through the public surface rather than the internal dictionary: what matters is what a
        // page would actually be shown.
        var translations = new Translations(new StubJSRuntime());
        translations.SetLanguageAsync(AppLanguage.Polish).GetAwaiter().GetResult();

        return KnownKeys.Select(key => new KeyValuePair<string, string>(key, translations[key])).ToList();
    }

    /// <summary>A spread of keys from each part of the app, so a whole section going missing is noticed.</summary>
    private static readonly string[] KnownKeys =
    [
        "Dashboard", "Notes", "Tasks", "Calendar", "Inventory", "Contacts", "Map", "Options",
        "Save", "Cancel", "Delete", "Add note", "Add task list", "Add event", "Add warehouse",
        "Log in", "Register", "Password", "Amount", "Priority", "Language", "Debugger",
        "Use my location", "Share link", "Write a message…", "Reply", "Forward"
    ];
}
