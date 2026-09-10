using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Localization;
using Orbit.Web.Components;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// The footer's two language codes. What matters about them is that they are a control rather than a
/// display: pressing one changes the language for the whole app, and the pair says which one is in
/// force without anybody pressing anything.
///
/// Nothing here is about the footer itself. It is in the layout, on every page, signed in or not -
/// which is the reason this moved out of Options and is checked by reading MainLayout rather than by a
/// test that would only be asserting where a component was written.
/// </summary>
public sealed class LanguagePickerTests : OrbitTestContext
{
    [Fact]
    public void It_marks_the_language_that_is_in_force()
    {
        var cut = RenderComponent<LanguagePicker>();

        var english = cut.Find("[lang=\"en\"]");
        var polish = cut.Find("[lang=\"pl\"]");
        Assert.Contains("chosen", english.ClassList);
        Assert.DoesNotContain("chosen", polish.ClassList);
        Assert.Equal("true", english.GetAttribute("aria-pressed"));
        Assert.Equal("false", polish.GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Pressing_the_other_one_switches_the_language()
    {
        var cut = RenderComponent<LanguagePicker>();

        cut.Find("[lang=\"pl\"]").Click();

        Assert.Equal(AppLanguage.Polish, Services.GetRequiredService<Translations>().Language);
        Assert.Contains("chosen", cut.Find("[lang=\"pl\"]").ClassList);
    }

    /// <summary>
    /// Pressing the one already in force does nothing at all - not even a store and a re-render. It is
    /// where somebody's finger lands when they are checking which language they are in.
    /// </summary>
    [Fact]
    public void Pressing_the_one_already_in_force_changes_nothing()
    {
        var cut = RenderComponent<LanguagePicker>();
        var languageChanges = 0;
        Services.GetRequiredService<Translations>().Changed += () => languageChanges++;

        cut.Find("[lang=\"en\"]").Click();

        Assert.Equal(0, languageChanges);
        Assert.Equal(AppLanguage.English, Services.GetRequiredService<Translations>().Language);
    }
}
