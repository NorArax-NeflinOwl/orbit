using System.Net;
using System.Net.Http.Json;
using System.Text;
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
/// The row of tabs the dashboard, the notes and the task lists are read under. The three built-in ones
/// are drawn here rather than fetched - they have no rows at all (see BuiltInFolder) - and the rest are
/// whatever the reader has made.
/// </summary>
public sealed class FolderTabsTests : OrbitTestContext
{
    private static readonly Guid WorkFolderId = Guid.NewGuid();
    private string? _createdFolderName;
    private readonly List<string> _deletedPaths = [];

    public FolderTabsTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
    }

    [Fact]
    public void The_three_built_in_folders_are_there_before_anybody_makes_one()
    {
        RegisterFolders([]);

        var cut = RenderComponent<FolderTabs>();

        Assert.Equal(["Public", "Private", "Finished"], TabNames(cut));
    }

    [Fact]
    public void A_folder_somebody_made_is_a_tab_after_the_built_in_ones()
    {
        RegisterFolders([AFolderCalled("Work")]);

        var cut = RenderComponent<FolderTabs>();

        Assert.Equal(["Public", "Private", "Finished", "Work"], TabNames(cut));
    }

    /// <summary>Public until somebody presses another - see FolderKey.Default.</summary>
    [Fact]
    public void The_page_opens_on_Public()
    {
        RegisterFolders([]);

        var cut = RenderComponent<FolderTabs>();

        Assert.Equal("Public", cut.Find(".folder-tab.on").TextContent.Trim());
    }

    [Fact]
    public void Pressing_a_tab_opens_it_for_every_page_at_once()
    {
        RegisterFolders([AFolderCalled("Work")]);
        var folders = Services.GetRequiredService<FolderState>();
        var cut = RenderComponent<FolderTabs>();

        cut.FindAll(".folder-tab").First(tab => tab.TextContent.Contains("Work")).Click();

        // The state is shared, which is what makes the tab still open on the next page - see FolderState.
        Assert.Equal(FolderKey.Of(WorkFolderId), folders.Chosen);
    }

    /// <summary>
    /// Making one is typed where the tab will be, and Enter is what says it is finished - a box that
    /// only answered a button somewhere else would be a box people press Enter in and wait.
    /// </summary>
    [Fact]
    public void Naming_a_new_folder_and_pressing_Enter_makes_it()
    {
        RegisterFolders([]);
        var cut = RenderComponent<FolderTabs>();

        cut.Find(".folder-tab-add").Click();
        cut.Find(".folder-tab-name").Input("Work");
        cut.Find(".folder-tab-name").KeyDown(Key.Enter);

        Assert.Equal("Work", _createdFolderName);
    }

    [Fact]
    public void A_name_that_is_only_spaces_makes_nothing()
    {
        RegisterFolders([]);
        var cut = RenderComponent<FolderTabs>();

        cut.Find(".folder-tab-add").Click();
        cut.Find(".folder-tab-name").Input("   ");
        cut.Find(".folder-tab-name").KeyDown(Key.Enter);

        Assert.Null(_createdFolderName);
    }

    /// <summary>The built-in three are nobody's to rename or throw away, so the menu is not offered on them.</summary>
    [Fact]
    public void A_built_in_folder_has_no_menu()
    {
        RegisterFolders([AFolderCalled("Work")]);

        var cut = RenderComponent<FolderTabs>();

        Assert.Empty(cut.FindAll(".overflow-menu-trigger"));
    }

    [Fact]
    public void A_folder_somebody_made_can_be_renamed_or_deleted()
    {
        RegisterFolders([AFolderCalled("Work")]);
        var cut = RenderComponent<FolderTabs>();

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
        RegisterFolders([AFolderCalled("Work")]);
        var cut = RenderComponent<FolderTabs>();
        cut.FindAll(".folder-tab").First(tab => tab.TextContent.Contains("Work")).Click();

        cut.Find(".overflow-menu-trigger").Click();
        cut.FindAll("button").First(button => button.TextContent.Contains("Delete folder")).Click();
        Assert.Contains("Nothing in it is deleted", cut.Markup);

        cut.FindAll(".dialog-footer button").First(button => button.TextContent.Contains("Delete folder")).Click();

        Assert.Contains(_deletedPaths, path => path.EndsWith($"/api/folders/{WorkFolderId}", StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> TabNames(IRenderedFragment cut)
        => [.. cut.FindAll(".folder-tab:not(.folder-tab-add)").Select(tab => tab.TextContent.Trim())];

    private static FolderDto AFolderCalled(string name)
        => new(WorkFolderId, name, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

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
                _createdFolderName = request.Content!.ReadFromJsonAsync<CreateFolderRequest>()
                    .GetAwaiter().GetResult()?.Name;
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = JsonContent.Create(AFolderCalled(_createdFolderName ?? string.Empty))
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
