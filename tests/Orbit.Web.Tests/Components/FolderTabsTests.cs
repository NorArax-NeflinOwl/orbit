using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Folders;
using Orbit.Core.Folders;
using Orbit.Web.Components;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// The row of tabs the dashboard, the notes and the task lists are read under. The built-in ones are
/// drawn here rather than fetched - they have no rows at all (see BuiltInFolder) - and the rest are
/// whatever the reader has made on that page.
/// </summary>
public sealed class FolderTabsTests : OrbitTestContext
{
    private static readonly Guid WorkFolderId = Guid.NewGuid();
    private CreateFolderRequest? _created;
    private readonly List<string> _deletedPaths = [];

    public FolderTabsTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
    }

    [Fact]
    public void The_built_in_folders_are_there_before_anybody_makes_one()
    {
        RegisterFolders([]);

        var cut = RenderTabs(FolderPage.Tasks);

        Assert.Equal(["Public", "Private", "Finished"], TabNames(cut));
    }

    /// <summary>
    /// A note has nothing to finish, so the tab could only ever have been empty there - which is what
    /// it was, and it read as a folder that had lost everything somebody put in it.
    /// </summary>
    [Fact]
    public void Only_the_task_lists_have_a_Finished_tab()
    {
        RegisterFolders([]);

        Assert.Equal(["Public", "Private"], TabNames(RenderTabs(FolderPage.Notes)));
        Assert.Equal(["Public", "Private"], TabNames(RenderTabs(FolderPage.Dashboard)));
    }

    [Fact]
    public void A_folder_somebody_made_is_a_tab_after_the_built_in_ones()
    {
        RegisterFolders([AFolderCalled("Work", FolderScope.Tasks)]);

        var cut = RenderTabs(FolderPage.Tasks);

        Assert.Equal(["Public", "Private", "Finished", "Work"], TabNames(cut));
    }

    /// <summary>A folder belongs to one page, so the other page does not draw it - see FolderScope.</summary>
    [Fact]
    public void A_folder_made_on_one_page_is_not_a_tab_on_the_other()
    {
        RegisterFolders([AFolderCalled("Work", FolderScope.Notes)]);

        Assert.Equal(["Public", "Private", "Work"], TabNames(RenderTabs(FolderPage.Notes)));
        Assert.Equal(["Public", "Private", "Finished"], TabNames(RenderTabs(FolderPage.Tasks)));
    }

    /// <summary>The dashboard shows both kinds of card, so it is read under both pages' tabs.</summary>
    [Fact]
    public void The_dashboard_draws_both_pages_folders()
    {
        RegisterFolders([AFolderCalled("Work", FolderScope.Notes), AnotherFolderCalled("Renovation", FolderScope.Tasks)]);

        var cut = RenderTabs(FolderPage.Dashboard);

        Assert.Equal(["Public", "Private", "Work", "Renovation"], TabNames(cut));
    }

    /// <summary>Public until somebody presses another - see FolderKey.Default.</summary>
    [Fact]
    public void The_page_opens_on_Public()
    {
        RegisterFolders([]);

        var cut = RenderTabs(FolderPage.Tasks);

        Assert.Equal("Public", cut.Find(".folder-tab.on").TextContent.Trim());
    }

    /// <summary>
    /// The tab is the page's own now. It used to be shared, so opening "Work" on the notes also opened
    /// a "Work" on the task lists that was a different folder holding different things.
    /// </summary>
    [Fact]
    public void Pressing_a_tab_opens_it_on_that_page_only()
    {
        RegisterFolders([AFolderCalled("Work", FolderScope.Tasks)]);
        var folders = Services.GetRequiredService<FolderState>();
        var cut = RenderTabs(FolderPage.Tasks);

        cut.FindAll(".folder-tab").First(tab => tab.TextContent.Contains("Work")).Click();

        Assert.Equal(FolderKey.Of(WorkFolderId), folders.ChosenOn(FolderPage.Tasks));
        Assert.Equal(FolderKey.Default, folders.ChosenOn(FolderPage.Notes));
    }

    /// <summary>
    /// A folder somebody made can be taken off the dashboard - a tab for recipes or receipts between
    /// Public and Private on the page you open to see what is on your plate. The menu that offers it is
    /// the folder's own, on the page the folder belongs to.
    /// </summary>
    [Fact]
    public void Hiding_a_folder_on_the_dashboard_is_offered_in_its_own_menu()
    {
        RegisterFolders([AFolderCalled("Recipes", FolderScope.Tasks)]);
        Services.GetRequiredService<FolderState>().Choose(FolderPage.Tasks, FolderKey.Of(WorkFolderId));
        var cut = RenderTabs(FolderPage.Tasks);

        cut.Find(".overflow-menu-trigger").Click();
        cut.FindAll(".avatar-dropdown-item")
            .First(entry => entry.TextContent.Contains("Hide on the dashboard", StringComparison.Ordinal)).Click();

        // What was written down, which is what the dashboard reads on its next render.
        var written = JSInterop.Invocations["setHiddenFolders"].Single();
        Assert.Contains(WorkFolderId.ToString(), Assert.IsAssignableFrom<IEnumerable<string>>(written.Arguments[0]!));
    }

    /// <summary>And it is then not a tab there, while staying one on the page it was made on.</summary>
    [Fact]
    public void A_folder_hidden_on_the_dashboard_is_not_a_tab_there()
    {
        RegisterFolders([AFolderCalled("Recipes", FolderScope.Tasks)]);
        HiddenOnTheDashboard(WorkFolderId);

        Assert.DoesNotContain("Recipes", TabNames(RenderTabs(FolderPage.Dashboard)));
        Assert.Contains("Recipes", TabNames(RenderTabs(FolderPage.Tasks)));
    }

    /// <summary>
    /// And the dashboard leaves a tab it can no longer draw: a page filtered to a folder nobody can see
    /// shows nothing and offers no way out of it.
    /// </summary>
    [Fact]
    public void The_dashboard_falls_back_to_Public_when_the_open_folder_is_hidden()
    {
        RegisterFolders([AFolderCalled("Recipes", FolderScope.Tasks)]);
        HiddenOnTheDashboard(WorkFolderId);
        var folders = Services.GetRequiredService<FolderState>();
        folders.Choose(FolderPage.Dashboard, FolderKey.Of(WorkFolderId));

        RenderTabs(FolderPage.Dashboard);

        Assert.Equal(FolderKey.Default, folders.ChosenOn(FolderPage.Dashboard));
    }

    /// <summary>What this device has taken off the dashboard, as the stored answer the row reads.</summary>
    private void HiddenOnTheDashboard(params Guid[] folderIds)
        => DashboardCards.Setup<string[]>("getHiddenFolders").SetResult([.. folderIds.Select(id => id.ToString())]);

    /// <summary>
    /// Making one is typed where the tab will be, and Enter is what says it is finished - a box that
    /// only answered a button somewhere else would be a box people press Enter in and wait.
    /// </summary>
    [Fact]
    public void Naming_a_new_folder_and_pressing_Enter_makes_it_on_this_page()
    {
        RegisterFolders([]);
        var cut = RenderTabs(FolderPage.Notes);

        cut.Find(".folder-tab-add").Click();
        cut.Find(".folder-tab-name").Input("Work");
        cut.Find(".folder-tab-name").KeyDown(Key.Enter);

        Assert.Equal("Work", _created?.Name);
        Assert.Equal(nameof(FolderScope.Notes), _created?.Scope);
    }

    [Fact]
    public void A_name_that_is_only_spaces_makes_nothing()
    {
        RegisterFolders([]);
        var cut = RenderTabs(FolderPage.Tasks);

        cut.Find(".folder-tab-add").Click();
        cut.Find(".folder-tab-name").Input("   ");
        cut.Find(".folder-tab-name").KeyDown(Key.Enter);

        Assert.Null(_created);
    }

    /// <summary>
    /// Nothing on the dashboard is filed from the dashboard, so a folder made there would be a tab
    /// nothing could ever go into - see FolderPages.MakesFoldersIn.
    /// </summary>
    [Fact]
    public void The_dashboard_offers_no_way_to_make_a_folder()
    {
        RegisterFolders([AFolderCalled("Work", FolderScope.Notes)]);

        var cut = RenderTabs(FolderPage.Dashboard);
        cut.FindAll(".folder-tab").First(tab => tab.TextContent.Contains("Work")).Click();

        Assert.Empty(cut.FindAll(".folder-tab-add"));
        Assert.Empty(cut.FindAll(".overflow-menu-trigger"));
    }

    /// <summary>The built-in ones are nobody's to rename or throw away, so the menu is not offered on them.</summary>
    [Fact]
    public void A_built_in_folder_has_no_menu()
    {
        RegisterFolders([AFolderCalled("Work", FolderScope.Tasks)]);

        var cut = RenderTabs(FolderPage.Tasks);

        Assert.Empty(cut.FindAll(".overflow-menu-trigger"));
    }

    [Fact]
    public void A_folder_somebody_made_can_be_renamed_or_deleted()
    {
        RegisterFolders([AFolderCalled("Work", FolderScope.Tasks)]);
        var cut = RenderTabs(FolderPage.Tasks);

        cut.FindAll(".folder-tab").First(tab => tab.TextContent.Contains("Work")).Click();

        Assert.Single(cut.FindAll(".overflow-menu-trigger"));
    }

    /// <summary>
    /// Deleting is asked about first, and what the question says is what actually happens: the tab goes
    /// and what was under it does not.
    /// </summary>
    [Fact]
    public void Deleting_a_folder_asks_first_and_then_removes_only_the_tab()
    {
        RegisterFolders([AFolderCalled("Work", FolderScope.Tasks)]);
        var cut = RenderTabs(FolderPage.Tasks);
        cut.FindAll(".folder-tab").First(tab => tab.TextContent.Contains("Work")).Click();

        cut.Find(".overflow-menu-trigger").Click();
        cut.FindAll("button").First(button => button.TextContent.Contains("Delete folder")).Click();
        Assert.Contains("Nothing in it is deleted", cut.Markup);

        cut.FindAll(".dialog-footer button").First(button => button.TextContent.Contains("Delete folder")).Click();

        Assert.Contains(_deletedPaths, path => path.EndsWith($"/api/folders/{WorkFolderId}", StringComparison.Ordinal));
    }

    private IRenderedComponent<FolderTabs> RenderTabs(FolderPage page)
        => RenderComponent<FolderTabs>(parameters => parameters.Add(tabs => tabs.Page, page));

    private static IReadOnlyList<string> TabNames(IRenderedFragment cut)
        => [.. cut.FindAll(".folder-tab:not(.folder-tab-add)").Select(tab => tab.TextContent.Trim())];

    private static FolderDto AFolderCalled(string name, FolderScope scope)
        => new(WorkFolderId, name, scope.ToString(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static FolderDto AnotherFolderCalled(string name, FolderScope scope)
        => new(Guid.NewGuid(), name, scope.ToString(), DateTimeOffset.UtcNow.AddMinutes(1), DateTimeOffset.UtcNow);

    private void RegisterFolders(IReadOnlyList<FolderDto> folders)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Delete)
            {
                _deletedPaths.Add(request.RequestUri!.AbsolutePath);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (request.Method == HttpMethod.Post)
            {
                _created = request.Content!.ReadFromJsonAsync<CreateFolderRequest>().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = JsonContent.Create(
                        AFolderCalled(_created?.Name ?? string.Empty, FolderScope.Tasks))
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(folders) };
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        Services.AddSingleton(new FolderState(new FoldersApiClient(httpClient)));
    }
}
