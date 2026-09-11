using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;

namespace Orbit.Web.Tests;

/// <summary>
/// A bUnit TestContext with the services every page needs regardless of what it is testing, so each
/// test class registers only what its own subject actually uses.
///
/// Translations is the case in point: every page reads its own text through it, so leaving it out fails
/// a test for a reason that has nothing to do with what the test is about. It runs in English here,
/// which is what the assertions are written against.
/// </summary>
public abstract class OrbitTestContext : TestContext
{
    /// <summary>
    /// The module the dashboard's layout is stored through - see DashboardCardPreferences. Held rather
    /// than set up and forgotten, so a test that wants a different stored answer can say so: setting the
    /// module up a second time replaces this one and takes the rest of its answers with it.
    /// </summary>
    protected BunitJSModuleInterop DashboardCards { get; private set; } = null!;

    protected OrbitTestContext()
    {
        Services.AddSingleton(new Translations(new StubJSRuntime()));
        Services.AddSingleton(SuggestingNothing());
        // Empty, which is what every page sees unless the map sent somebody to it - the same reason
        // Translations is here. A test about the handover puts a place in it first.
        Services.AddSingleton(new ChosenPlace());
        // The inventory page reads the order this reader put their inventories in. StubJSRuntime is
        // the one that answers localStorage, and it starts empty - so nothing is arranged, which is
        // the right answer for a test that has not arranged anything.
        Services.AddSingleton(new InventoryArrangement(new StubJSRuntime()));
        // The contacts page reads which conversations this reader keeps at the top. Same storage, same
        // empty start - nothing is pinned until a test pins something.
        Services.AddSingleton(new ConversationPins(new StubJSRuntime()));
        // The tabs every page made of cards is drawn under. Nobody has made a folder, which is what a
        // fresh account looks like: the three built-in ones are still there, and everything is in the
        // one that opens. A test about folders registers its own over this - see FolderState.
        Services.AddSingleton(new FolderState(new FoldersApiClient(
            new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            }))
            {
                BaseAddress = new Uri("https://example.test/")
            })));
        // The bell's shared unread set. Every section page listens to it now - a thing somebody has just
        // shared is one the page has never read, so hearing about it is what makes it re-read - and a
        // page cannot be rendered at all without one. Empty unless a test puts something in it, which is
        // the right answer for a test that has not.
        Services.AddSingleton(new NotificationFeedState());
        // Marking the bell's entries read once a page is reached. Registered here for the same reason
        // Translations is: several pages settle their own news now, and a test about what a page shows
        // should not fail on a service it never exercises. It answers every request with "nothing to
        // mark", which is what a page with an empty bell in front of it should see - a test that is
        // about the settling registers its own.
        Services.AddScoped(services => new NewsSettler(
            new NotificationsApiClient(new HttpClient(
                new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent)))
            {
                BaseAddress = new Uri("https://example.test/")
            }),
            services.GetRequiredService<NotificationFeedState>()));
        // The questions asked before a task list is deleted, which three pages now inject - see
        // TaskListDeletion. Registered here for the same reason Translations is: a test about what a
        // page shows should not fail on a service it never exercises. It resolves the TasksApiClient
        // the test itself registered, so a test that does exercise it still drives its own stub.
        Services.AddScoped<TaskListDeletion>();
        // Ticking one entry of a task list off, which the checklist and the entry's own page both do
        // through this now - see TaskItemCompletion. Registered here for the same reason
        // TaskListDeletion is: it resolves the TasksApiClient the test itself registered, so a test
        // that does tick something still drives its own stub.
        Services.AddScoped<TaskItemCompletion>();
        // Which parts of the dashboard this device has put away, and which folders it keeps off it -
        // the row of folder tabs asks the second on every page that carries it, so a test about the
        // tabs should not fail on a service it never exercises. Nothing is hidden here, which is what a
        // fresh browser looks like; the dashboard's own tests register theirs over this one.
        DashboardCards = JSInterop.SetupModule("./js/dashboardCards.js");
        DashboardCards.Setup<string[]>("getHiddenCards").SetResult([]);
        DashboardCards.SetupVoid("setHiddenCards", _ => true).SetVoidResult();
        DashboardCards.Setup<Dictionary<string, string>>("getCardFilters").SetResult([]);
        DashboardCards.SetupVoid("setCardFilters", _ => true).SetVoidResult();
        DashboardCards.Setup<string[]>("getHiddenFolders").SetResult([]);
        DashboardCards.SetupVoid("setHiddenFolders", _ => true).SetVoidResult();
        Services.AddScoped<DashboardCardPreferences>();
        // Every overflow menu asks JS to place it inside the viewport when it opens - see
        // OverflowMenu and menuAnchor.js. There is no layout to measure here, so it answers and does
        // nothing; without it any test that opens a menu fails on the interop call rather than on
        // whatever it was about.
        JSInterop.SetupModule("./js/menuAnchor.js").SetupVoid("anchorToTrigger", _ => true).SetVoidResult();
        // How every editor and summary finishes - see NavigationTrail. Made when the page asks for it,
        // so its trail starts wherever the test has navigated to by then: a test about where a screen
        // ends navigates first. Stepping back is a call into the browser, answered here and read back
        // with JSInterop.VerifyInvoke("history.go"); the address does not move, since there is no
        // browser here to move it.
        Services.AddScoped<NavigationTrail>();
        JSInterop.SetupVoid("history.go", _ => true).SetVoidResult();
        // The one field a task list and an inventory are named in draws itself through a module too - see
        // ChecklistTextEditor, which the note editor and TitledDescription both use. Same reason as the
        // menu above: without this, every editor test fails on an interop call rather than on whatever
        // it was about. Answered rather than made loose, so a call nobody expected is still an error.
        var checklistEditor = JSInterop.SetupModule("./js/checklistTextEditor.js");
        checklistEditor.SetupVoid("initialize", _ => true).SetVoidResult();
        checklistEditor.SetupVoid("insertChecklistItem", _ => true).SetVoidResult();
        checklistEditor.SetupVoid("dispose", _ => true).SetVoidResult();
        // Nothing was typed into a surface that does not exist, so it reports the lines it was given.
        checklistEditor.Setup<string>("getLinesAsJson", _ => true).SetResult("[]");
        RegisterSharingWithNobody();
    }

    /// <summary>
    /// What every page that can hand something to somebody needs in order to render at all: who is
    /// signed in, who they know, and the sealed sender an invitation goes out through. Nobody and
    /// nothing here - the same reason <see cref="SuggestingNothing"/> is registered above. A test about
    /// sharing registers its own over these, and the later registration is the one that resolves.
    ///
    /// It sits in the base because sharing stopped being one page's business: an inventory is handed on
    /// from its card and from inside its own editor now, so four test classes were failing on a service
    /// none of them were about.
    /// </summary>
    private void RegisterSharingWithNobody()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        // No token was ever stored, so this answers "nobody is signed in" - which is what a test that
        // has not signed anybody in should see.
        var tokenStore = new TokenStore(new StubJSRuntime());
        var authenticationStateProvider = new OrbitAuthenticationStateProvider(
            tokenStore, new TokenRefreshService(tokenStore, httpClient));
        Services.AddSingleton(authenticationStateProvider);

        var usersApiClient = new UsersApiClient(httpClient);
        // Answers "nothing shared with them", which is what a test that has shared nothing should see -
        // the same reason the rest of this method registers empty. A test about the list registers its
        // own over this. It is here because the contact card reads it on every load now.
        Services.AddSingleton(new SharesApiClient(httpClient));
        Services.AddSingleton(new ChatApiClient(httpClient));
        Services.AddSingleton(new EncryptedChatMessageSender(
            JSInterop.JSRuntime,
            new OwnEncryptionKeyProvider(JSInterop.JSRuntime, usersApiClient, authenticationStateProvider),
            usersApiClient,
            new ChatApiClient(httpClient)));
    }

    /// <summary>
    /// Name suggestions that never suggest anything. Every editor now carries the control that asks for
    /// them, so leaving this out fails an editor test for a reason that has nothing to do with what the
    /// test is about - the same reason Translations is here. A test that is actually about suggestions
    /// registers its own client over this one.
    /// </summary>
    private static NameSuggestionsApiClient SuggestingNothing()
        => new(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        });
}
