using Orbit.Core.Abstractions;
using Orbit.Localization;
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
    public void English_writes_dates_in_English()
    {
        var translations = new Translations(new StubJSRuntime());

        Assert.Equal("Monday", new DateTime(2026, 3, 2).ToString("dddd", translations.DisplayCulture));
    }

    [Fact]
    public async Task Polish_writes_dates_in_Polish()
    {
        var translations = new Translations(new StubJSRuntime());

        await translations.SetLanguageAsync(AppLanguage.Polish);

        // Reading an interface in Polish and being told "Monday, March 2" is only half a translation.
        Assert.Equal("poniedziałek", new DateTime(2026, 3, 2).ToString("dddd", translations.DisplayCulture));
        Assert.Equal("marzec", translations.DisplayCulture.DateTimeFormat.GetMonthName(3));
    }

    [Fact]
    public async Task The_display_culture_is_never_used_for_a_number_a_machine_reads()
    {
        var translations = new Translations(new StubJSRuntime());
        await translations.SetLanguageAsync(AppLanguage.Polish);

        // Polish writes 50,06 with a comma. That is right for a person and wrong for a URL, which is why
        // coordinates and link stamps format against InvariantCulture instead - see MapPage's
        // CoordinateCulture and GoogleMapsLink.
        Assert.Equal("50,06", 50.06.ToString("F2", translations.DisplayCulture));
        Assert.Equal("50.06", 50.06.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
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
        var untranslated = PolishTranslationsUnderTest()
            .Where(pair => pair.Key == pair.Value && !DeliberatelyIdentical.Contains(pair.Key))
            .Select(pair => pair.Key)
            .ToList();

        Assert.Empty(untranslated);
    }

    [Fact]
    public void Every_label_a_dropdown_holds_is_translated()
    {
        AssertAllTranslated(NotificationChannelOption.All.Select(option => option.Label));
    }

    [Fact]
    public void Every_way_a_share_can_be_worded_is_translated()
    {
        AssertAllTranslated(Enum.GetValues<ShareAccessLevel>()
            .Select(level => SharedItemAccess.For(isShared: true, level.ToString()).Description));
    }

    [Fact]
    public void Every_stand_in_an_api_client_substitutes_is_translated()
    {
        // The API clients put these in front of the reader in place of something that can't be read or
        // named. By the time a page renders one it looks like any other title, so it has to be
        // translated where it is substituted - see NotesApiClient.Translated.
        AssertAllTranslated([
            NotesApiClient.UnreadableNoteTitle,
            TasksApiClient.UnreadableTaskListTitle,
            InventoryApiClient.UnreadableWarehouseName,
            "another user",
            "another list"
        ]);
    }

    [Fact]
    public async Task A_sentence_with_a_value_in_it_is_translated_whole()
    {
        var translations = new Translations(new StubJSRuntime());
        await translations.SetLanguageAsync(AppLanguage.Polish);

        // The placeholder travels with the sentence rather than the sentence being glued together
        // around it, because Polish does not put the pieces in English's order.
        Assert.Equal(
            "Ala właśnie edytuje tę notatkę — spróbuj za chwilę.",
            translations.Format("{0} is currently editing this note - try again in a moment.", "Ala"));
    }

    private static IReadOnlyList<KeyValuePair<string, string>> PolishTranslationsUnderTest()
    {
        // Read through the public surface rather than the internal dictionary: what matters is what a
        // page would actually be shown.
        var translations = new Translations(new StubJSRuntime());
        translations.SetLanguageAsync(AppLanguage.Polish).GetAwaiter().GetResult();

        return KnownKeys.Select(key => new KeyValuePair<string, string>(key, translations[key])).ToList();
    }

    /// <summary>Words Polish spells exactly as English does - anything else matching its key is an
    /// entry someone added and forgot to translate.</summary>
    private static readonly string[] DeliberatelyIdentical =
        ["Min", "Debugger", "Release", "Debug", "Import", "Export", "Push"];

    /// <summary>
    /// Asserts each of these reads as Polish. Written as one helper because all three callers hold keys
    /// that reach T[...] as a variable rather than as a literal - nothing reading the source can tell
    /// they are keys at all, so a missing one shows English inside an otherwise Polish page and no
    /// source sweep would ever find it.
    /// </summary>
    private static void AssertAllTranslated(IEnumerable<string> keys)
    {
        var translations = new Translations(new StubJSRuntime());
        translations.SetLanguageAsync(AppLanguage.Polish).GetAwaiter().GetResult();

        Assert.All(
            keys.Where(key => !DeliberatelyIdentical.Contains(key)),
            key => Assert.NotEqual(key, translations[key]));
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
