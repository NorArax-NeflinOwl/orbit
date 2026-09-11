using Bunit;
using Orbit.Core.Folders;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// A dashboard card's filter belongs to the folder tab it was chosen under - "only what is pinned" is a
/// thing somebody wants of one folder and not of every other - and a filter chosen before tabs had their
/// own must keep applying where it was chosen, on the tab the dashboard opens on.
/// </summary>
public sealed class DashboardCardPreferencesTests : OrbitTestContext
{
    private static readonly Guid WorkFolderId = Guid.NewGuid();
    private static readonly Guid HomeFolderId = Guid.NewGuid();

    [Fact]
    public async Task A_filter_stored_before_tabs_had_their_own_still_applies_to_Public()
    {
        var preferences = await PreferencesStoring(new() { ["notes"] = nameof(DashboardCardFilter.Pinned) });

        Assert.Equal(DashboardCardFilter.Pinned, preferences.FilterFor("notes", FolderKey.Default));
        Assert.Equal(DashboardCardFilter.All, preferences.FilterFor("notes", FolderKey.Of(BuiltInFolder.Private)));
    }

    [Fact]
    public async Task A_filter_chosen_under_one_folder_leaves_every_other_tab_alone()
    {
        var preferences = await PreferencesStoring([]);

        await preferences.SetFilterAsync("notes", FolderKey.Of(WorkFolderId), DashboardCardFilter.HighPriority);

        Assert.Equal(DashboardCardFilter.HighPriority, preferences.FilterFor("notes", FolderKey.Of(WorkFolderId)));
        Assert.Equal(DashboardCardFilter.All, preferences.FilterFor("notes", FolderKey.Of(HomeFolderId)));
        Assert.Equal(DashboardCardFilter.All, preferences.FilterFor("notes", FolderKey.Default));
        Assert.Equal(DashboardCardFilter.All, preferences.FilterFor("tasks", FolderKey.Of(WorkFolderId)));
    }

    [Fact]
    public async Task Showing_everything_again_forgets_that_tabs_filter_only()
    {
        var preferences = await PreferencesStoring(new()
        {
            ["notes"] = nameof(DashboardCardFilter.Pinned),
            ["notes@Private"] = nameof(DashboardCardFilter.LowPriority)
        });

        await preferences.SetFilterAsync("notes", FolderKey.Of(BuiltInFolder.Private), DashboardCardFilter.All);

        Assert.Equal(DashboardCardFilter.All, preferences.FilterFor("notes", FolderKey.Of(BuiltInFolder.Private)));
        Assert.Equal(DashboardCardFilter.Pinned, preferences.FilterFor("notes", FolderKey.Default));
    }

    /// <summary>The preferences as a browser holding these stored filters would load them.</summary>
    private async Task<DashboardCardPreferences> PreferencesStoring(Dictionary<string, string> storedFilters)
    {
        var module = JSInterop.SetupModule("./js/dashboardCards.js");
        module.Setup<string[]>("getHiddenCards").SetResult([]);
        module.Setup<string[]>("getHiddenFolders").SetResult([]);
        module.Setup<Dictionary<string, string>>("getCardFilters").SetResult(storedFilters);
        module.SetupVoid("setCardFilters", _ => true).SetVoidResult();

        var preferences = new DashboardCardPreferences(JSInterop.JSRuntime);
        await preferences.InitializeAsync();
        return preferences;
    }
}
