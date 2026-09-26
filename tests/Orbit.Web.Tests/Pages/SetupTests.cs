using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Folders;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Tasks;
using Orbit.Contracts.Users;
using Orbit.Core.Folders;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// The page the folders and the filters are made on, asked for on 2026-09-24. What it is for is having
/// all five kinds of folder in front of the reader at once: a tab row can only ever show one kind, and
/// "where do I rename the one I made last month" used to have five answers.
/// </summary>
public sealed class SetupTests : OrbitTestContext
{
    public SetupTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
    }

    /// <summary>What the page asked the server to make, when it asked - see TagFilterDialog.</summary>
    private CreateTaskTagFilterRequest? _madeFilter;

    /// <summary>The folders the server answers with, and whatever the page has since made.</summary>
    private readonly List<FolderDto> _folders = [];

    private IReadOnlyList<NoteDto> _notes = [];
    private IReadOnlyList<TaskDto> _taskLists = [];

    /// <summary>
    /// Every kind is a section of its own, the map included - it is the only page that says so, the map
    /// having had no folders at all until 2026-09-26.
    /// </summary>
    [Fact]
    public void Every_kind_of_folder_has_a_section_of_its_own()
    {
        RegisterApiClients();

        var cut = RenderComponent<Web.Pages.Setup>();

        var kinds = cut.FindAll("h3.options-heading").Select(heading => heading.TextContent.Trim());
        Assert.Equal(["Notes", "Tasks", "Calendar", "Inventory", "Map"], kinds);
    }

    /// <summary>A folder made here is a tab on the page its kind is read on - see FolderPages.MakesFoldersIn.</summary>
    [Fact]
    public void A_folder_is_made_under_the_kind_it_is_made_beside()
    {
        RegisterApiClients();
        var cut = RenderComponent<Web.Pages.Setup>();

        // Indexed through LINQ rather than by [2]: bUnit's refreshable collection indexer is missing on
        // the AngleSharp this project resolves, so a subscript throws MissingMethodException.
        // And found again after the typing, because the input re-renders the page and takes the button's
        // event handler with it - see UnknownEventHandlerIdException.
        CalendarCard(cut).QuerySelectorAll("input").Last().Input("Holiday");
        CalendarCard(cut).QuerySelectorAll("button").Last().Click();

        var made = Assert.Single(_folders);
        Assert.Equal("Holiday", made.Name);
        Assert.Equal(nameof(FolderScope.Calendar), made.Scope);
    }

    /// <summary>The calendar's section - third of the five, in the order the navigation reads them.</summary>
    private static AngleSharp.Dom.IElement CalendarCard(IRenderedComponent<Web.Pages.Setup> cut)
        => cut.FindAll(".options-card").Skip(2).First();

    /// <summary>
    /// Only an empty folder can go, which is the rule the tab rows have followed since 2026-09-20 - and
    /// this page can check it for every kind at once, having read all five. What is in it is drawn
    /// beside the name, so the greyed button is not a refusal with no reason.
    /// </summary>
    [Fact]
    public void A_folder_holding_something_cannot_be_deleted_and_says_how_much_it_holds()
    {
        var work = new FolderDto(
            Guid.NewGuid(), "Work", nameof(FolderScope.Notes), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _folders.Add(work);
        _notes = [Note("Shopping", work.Id), Note("Invoices", work.Id)];
        RegisterApiClients();

        var cut = RenderComponent<Web.Pages.Setup>();

        var notes = cut.FindAll(".options-card").First();
        Assert.Equal("2", notes.QuerySelector(".card-count")!.TextContent.Trim());
        var delete = notes.QuerySelectorAll("button").First(button => button.TextContent.Contains("Delete folder"));
        Assert.True(delete.HasAttribute("disabled"));
    }

    /// <summary>
    /// "Create filter" is here since 2026-09-24 rather than on the task lists: it offers the tags already
    /// on the lists as a checklist, a new word ticked as it is added, and "And" to need every tag - and
    /// Save sends exactly that. Moved from TasksTests, which is where that page used to make one.
    /// </summary>
    [Fact]
    public void A_filter_is_made_from_ticked_tags_a_new_one_and_the_and_switch()
    {
        _taskLists =
        [
            TaskList("Groceries") with { Tags = ["shopping"] },
            TaskList("Hall") with { Tags = ["home", "Shopping"] }
        ];
        RegisterApiClients();
        var cut = RenderComponent<Web.Pages.Setup>();

        cut.FindAll("button").First(button => button.TextContent.Trim() == "Create filter").Click();
        var offered = cut.FindAll(".tag-filter-list label").Select(label => label.TextContent.Trim());
        Assert.Equal(["home", "shopping"], offered);

        cut.FindAll(".tag-filter-list label").First(label => label.TextContent.Contains("home"))
            .QuerySelector("input")!.Change(true);
        cut.Find(".tag-filter-new input").Input("garden");
        cut.FindAll(".tag-filter-new button").Single().Click();
        cut.Find(".tag-filter-and").Click();
        cut.Find(".dialog-footer button[aria-label='Save']").Click();

        var body = Assert.IsType<CreateTaskTagFilterRequest>(_madeFilter);
        Assert.Equal(["home", "garden"], body.Tags);
        Assert.True(body.MatchesAll);
        Assert.Empty(cut.FindAll(".tag-filter-dialog"));
        // And it is listed where it was made, by the words it looks for - a filter has no other name.
        Assert.Contains("home and garden", cut.Markup);
    }

    private void RegisterApiClients()
    {
        Services.AddSingleton(new FolderState(new FoldersApiClient(Http(request =>
            request.Method == HttpMethod.Post ? MadeFolder(request) : Answer(_folders)))));
        Services.AddSingleton(new NotesApiClient(Http(_ => Answer(_notes))));
        Services.AddSingleton(new TasksApiClient(Http(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/api/task-filters", StringComparison.Ordinal)
                ? request.Method == HttpMethod.Post ? MadeFilter(request) : Answer(Array.Empty<TaskTagFilterDto>())
                : Answer(_taskLists))));
        Services.AddSingleton(new CalendarApiClient(Http(_ => Answer(Array.Empty<object>()))));
        Services.AddSingleton(new InventoryApiClient(Http(_ => Answer(Array.Empty<object>()))));
        Services.AddSingleton(new PlacesApiClient(Http(_ => Answer(Array.Empty<object>()))));

        // The account, with the map unlocked - the Map section is drawn only for an account that may use
        // the map at all, the same gate the page itself is behind (ApplicationPermission.Location).
        var usersApiClient = new UsersApiClient(Http(_ => Answer(new UserPermissionsDto(["Location"]))));
        Services.AddSingleton(usersApiClient);
        Services.AddSingleton(new UserPermissionState(usersApiClient));
    }

    /// <summary>A folder the page asked for, kept so the next read answers with it - the server's own job.</summary>
    private HttpResponseMessage MadeFolder(HttpRequestMessage request)
    {
        var asked = request.Content!.ReadFromJsonAsync<CreateFolderRequest>().GetAwaiter().GetResult()!;
        var made = new FolderDto(
            Guid.NewGuid(), asked.Name, asked.Scope, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _folders.Add(made);
        return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(made) };
    }

    private HttpResponseMessage MadeFilter(HttpRequestMessage request)
    {
        var made = _madeFilter = request.Content!.ReadFromJsonAsync<CreateTaskTagFilterRequest>()
            .GetAwaiter().GetResult()!;
        return new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(
                new TaskTagFilterDto(Guid.NewGuid(), made.Tags, made.MatchesAll, DateTimeOffset.UtcNow))
        };
    }

    private static HttpResponseMessage Answer<T>(T body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private static HttpClient Http(Func<HttpRequestMessage, HttpResponseMessage> answer)
        => new(new StubHttpMessageHandler(answer)) { BaseAddress = new Uri("https://example.test/") };

    private static NoteDto Note(string title, Guid folderId)
        => new(
            Guid.NewGuid(), title, [], IsPrivate: false, EncryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null,
            FolderId: folderId);

    private static TaskDto TaskList(string title)
        => new(
            Guid.NewGuid(), title, [], IsCompleted: false, IsGroup: false, IsPrivate: false,
            EncryptedContent: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);
}
