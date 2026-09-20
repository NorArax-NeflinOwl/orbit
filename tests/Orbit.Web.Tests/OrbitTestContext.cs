using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Contracts.Notifications;
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
        // The machine's own clock, because most tests build their data from the real "now". A test about
        // a page whose answer changes with the hour registers a FakeTimeProvider over this one.
        Services.AddSingleton(TimeProvider.System);
        // What this browser keeps about itself - the task editor asks which kinds of entry a picked name
        // fills in. Nothing stored, so every kind: a fresh browser. A test about a setting registers its own.
        Services.AddSingleton(new DevicePreferences(new StubJSRuntime()));
        // The places a task list's Location entries keep, which every task editor save now asks about.
        // Nobody keeps any here and nothing is found for any address, so a save makes none - a test that
        // is about those places registers its own over this.
        Services.AddScoped(_ => new TaskEntryPlaces(
            new PlacesApiClient(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            }))
            {
                BaseAddress = new Uri("https://example.test/")
            }),
            new GeocodingApiClient(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            }))
            {
                BaseAddress = new Uri("https://geocode.test/")
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TaskEntryPlaces>.Instance));
        Services.AddSingleton(SuggestingNothing());
        // Every card and editor that draws a tag asks the account's colours - see TagColourBook. None by
        // default; a test about colours registers a client that answers some.
        Services.AddSingleton(new TagsApiClient(new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
            }))
        {
            BaseAddress = new Uri("https://example.test/")
        }));
        Services.AddScoped<TagColourBook>();
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
        // How this account can be reached, and marking the bell's entries read once a page is reached.
        // Registered here for the same reason Translations is: several pages settle their own news and
        // several ask which notification channels to offer ungreyed (see GenerateInventoryOverlay), and
        // a test about what a page draws should not fail on a service it never exercises. Nothing to
        // mark, and an account that can be reached every way - which is what a fresh one looks like. A
        // test about either registers its own over this.
        Services.AddScoped(_ => new NotificationsApiClient(new HttpClient(
            new StubHttpMessageHandler(request => request.Method == HttpMethod.Get
                // An account that can be reached every way, which is what a fresh one looks like. A
                // 204 here would be a body the client cannot read: the settings endpoint always
                // answers with settings.
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new NotificationSettingsDto(
                        AllowNotifications: true, AllowPush: true, AllowEmail: true, AllowMobileBanner: true,
                        ShowExceptionDetails: false, BannerVisibleSeconds: 5, BannerMinimumGapSeconds: 5))
                }
                : new HttpResponseMessage(HttpStatusCode.NoContent)))
        {
            BaseAddress = new Uri("https://example.test/")
        }));
        Services.AddScoped(services => new NewsSettler(
            services.GetRequiredService<NotificationsApiClient>(),
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
        // What has been written in a note and not saved - see NoteDrafts. The editor keeps and reads it
        // on every load, so a test about that page should not fail on a service it never exercises.
        // Empty here, which is a browser where nothing has been left unsaved.
        Services.AddScoped<NoteDrafts>();
        // Whether the PIN in front of what is private has been answered - see PrivatePinGate. Every
        // page that draws something sealed asks it on load, so a test about one of those pages should
        // not fail on a service it never exercises. The account it reads has no PIN unless a test
        // registers one that has, which is the door standing open.
        Services.AddScoped(services => new PrivatePinGate(
            services.GetService<UsersApiClient>()
            ?? new UsersApiClient(new HttpClient(new StubHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.NotFound)))
            {
                BaseAddress = new Uri("https://example.test/")
            })));
        // Which of the two the page is in. The map asks it to start its own light/night switch from
        // what the map already looks like (see ThemeService.IsDarkNowAsync), so a test about the map
        // should not fail on a service it never exercises - the same reason Translations is here.
        // Nothing was stored and the browser here is a light one, which is what a fresh browser gets.
        Services.AddScoped<ThemeService>();
        JSInterop.SetupModule("./js/theme.js").Setup<bool>("systemPrefersDark").SetResult(false);
        // And the panel that belongs to a field rather than to a button asks the same module to place
        // it - the suggestions under a box, and the browser a vocabulary is chosen in (ValueBrowser).
        var menuAnchor = JSInterop.SetupModule("./js/menuAnchor.js");
        menuAnchor.SetupVoid("anchorToTrigger", _ => true).SetVoidResult();
        menuAnchor.SetupVoid("anchorToField", _ => true).SetVoidResult();
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
