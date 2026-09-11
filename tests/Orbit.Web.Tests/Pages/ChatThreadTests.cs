using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Orbit.Contracts.Chat;
using Orbit.Contracts.Notifications;
using Orbit.Core.LiveUpdates;
using Orbit.Core.Permissions;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// The chat thread, which had no coverage at all - recorded as a testing gap on the grounds that it is
/// "a polling component whose interesting behaviour is timing". That is exactly why it is worth having:
/// the two decisions this loop makes are both invisible when they break. A poll that stops honouring
/// the tab's visibility costs money and battery and looks identical from the screen; a poll that reads
/// the whole roster on every tick was two thirds of this page's traffic and looked identical too.
///
/// The loop runs on a real one-second <c>PeriodicTimer</c>, so these tests wait real seconds rather
/// than pretending. There is no seam to shorten it and inventing one for the tests would be testing the
/// seam - what is asserted here is what the deployed page does.
///
/// An announcement over the live connection is delivered through <c>LiveUpdatesConnection.Announce</c>,
/// the method the hub's own handlers call, so the page's answer to one is driven rather than assumed.
/// What is deliberately not covered: the slower pace behind a connection that is really up, and the
/// encryption, which is checked in a real browser by ci/verify-browser-crypto.mjs.
/// </summary>
public sealed class ChatThreadTests : OrbitTestContext
{
    private static readonly Guid OwnUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    /// <summary>
    /// Long enough that a running loop would certainly have ticked. Only the tests asserting that
    /// nothing happens wait a fixed time; the rest wait for the loop itself - see TheLoopHasTicked.
    /// </summary>
    private static readonly TimeSpan LongerThanATick = TimeSpan.FromSeconds(1.6);

    /// <summary>
    /// How long to give the loop to reach a tick count. Generous because it is a ceiling that is never
    /// reached on a healthy run - the wait ends as soon as the condition holds.
    /// </summary>
    private static readonly TimeSpan EnoughForAFewTicks = TimeSpan.FromSeconds(15);

    /// <summary>How many times each path has been asked for, counted across every stubbed client.</summary>
    private readonly Dictionary<string, int> _requestsByPath = [];

    private readonly object _countingLock = new();

    /// <summary>The live connection the page subscribes to - never connected, and announced into by hand.</summary>
    private LiveUpdatesConnection _liveUpdates = null!;

    /// <summary>Whether this tab is in front of somebody - what ./js/presence.js answers.</summary>
    private bool _isPageVisible = true;

    /// <summary>
    /// Whether the API knows the account this conversation is with. False is the case the page has to
    /// say something about rather than open an empty thread for.
    /// </summary>
    private bool _theOtherAccountResolves = true;

    /// <summary>Whether the tab is visible and the window has focus - what ./js/chatSeen.js answers to isInFront.</summary>
    private bool _isInFront = true;

    /// <summary>The newest message whose end is on screen - what ./js/chatSeen.js answers to newestInView.</summary>
    private Guid? _newestInView;

    /// <summary>What the conversation read answers with. Empty unless a test put something in it.</summary>
    private readonly List<ChatMessageDto> _conversation = [];

    /// <summary>Every "read up to" the page sent, in order; null for a mark sent without one.</summary>
    private readonly List<DateTimeOffset?> _readsSent = [];

    private BunitJSModuleInterop _seenModule = null!;

    private static readonly DateTimeOffset TheStartOfTheTest = new(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);

    public ChatThreadTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterPermissions();
        RegisterTheApi(RegisterAuthentication());
        TheBrowserCanSealAndOpen();
    }

    /// <summary>
    /// The rule that costs real money when it goes: nothing is asked for on behalf of a tab sitting
    /// behind thirty others. The initial load still happens - somebody opened the page - and then the
    /// loop ticks and does nothing at all.
    ///
    /// Waiting on the loop's own ticks rather than on the clock is what makes this an assertion at
    /// all: "nothing was polled" is also true of a loop that never started, and a fixed delay cannot
    /// tell the two apart.
    /// </summary>
    [Fact]
    public async Task Nothing_is_polled_while_the_tab_is_behind_other_tabs()
    {
        _isPageVisible = false;
        RenderTheConversation();
        var afterTheFirstLoad = TimesAsked("api/chat/messages");

        await WaitUntilTheLoopHasTickedAsync(times: 3);

        Assert.Equal(afterTheFirstLoad, TimesAsked("api/chat/messages"));
    }

    /// <summary>The other half of it, so the test above cannot pass on a page that asks for nothing ever.</summary>
    [Fact]
    public async Task A_tab_somebody_is_looking_at_is_polled()
    {
        RenderTheConversation();
        var afterTheFirstLoad = TimesAsked("api/chat/messages");

        await WaitUntilTheLoopHasTickedAsync(times: 1);

        Assert.True(
            TimesAsked("api/chat/messages") > afterTheFirstLoad,
            $"The conversation was read {TimesAsked("api/chat/messages")} times, which is no more "
                + $"than the {afterTheFirstLoad} the first load accounts for.");
    }

    /// <summary>
    /// The roster is not read on every tick. Who this account talks to changes on the scale of days,
    /// and asking for every contact and every group once a second was two thirds of this loop's
    /// traffic spent on an answer that had not changed.
    ///
    /// Twice is the whole rule: once as the page loads, and once on the first tick - the counter
    /// starts at its limit so a conversation started a moment ago appears in the drawer without
    /// waiting ten seconds for it. The nine ticks after that ask for nothing. The messages are read on
    /// every one of those ticks, which is what says the loop was running while the roster was not
    /// being asked for.
    /// </summary>
    [Fact]
    public async Task The_conversation_list_is_read_once_in_ten_ticks_rather_than_on_every_one()
    {
        RenderTheConversation();

        await WaitUntilTheLoopHasTickedAsync(times: 3);

        Assert.Equal(2, TimesAsked("api/chat/contacts"));
        Assert.Equal(2, TimesAsked("api/chat/groups"));
        Assert.True(
            TimesAsked("api/chat/messages") > TimesAsked("api/chat/contacts"),
            $"The conversation was read {TimesAsked("api/chat/messages")} times and the roster "
                + $"{TimesAsked("api/chat/contacts")} - the two are meant to run at different paces.");
    }

    /// <summary>
    /// Leaving the page stops the loop. Without this a reader who opens three conversations in a
    /// minute leaves three timers behind, each still asking for a thread nobody is looking at - and
    /// nothing on screen would say so.
    ///
    /// Asserted on the tick rather than on the request, so it holds for the hidden-tab case too: a
    /// loop still running behind a hidden tab asks for nothing either, and only the tick tells them
    /// apart.
    /// </summary>
    [Fact]
    public async Task Leaving_the_page_stops_the_polling()
    {
        var cut = RenderTheConversation();
        await WaitUntilTheLoopHasTickedAsync(times: 1);

        await cut.Instance.DisposeAsync();
        var whenItWasLeft = await TicksOnceTheLastOneHasLandedAsync();
        await Task.Delay(LongerThanATick);

        Assert.Equal(whenItWasLeft, TicksSoFar());
    }

    /// <summary>
    /// A group takes the thread on this same page, and a group's messages are GroupConversation's to
    /// load - so the one-to-one loop has to stop when one opens. It used to be possible to leave it
    /// running against a conversation that is no longer on screen.
    /// </summary>
    [Fact]
    public async Task Opening_a_group_stops_the_loop_the_person_left_running()
    {
        var cut = RenderTheConversation();
        await WaitUntilTheLoopHasTickedAsync(times: 1);
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        navigationManager.NavigateTo("/chat/groups");
        cut.SetParametersAndRender(parameters => parameters.Add(page => page.UserId, Guid.Empty));
        var whenTheGroupOpened = await TicksOnceTheLastOneHasLandedAsync();
        await Task.Delay(LongerThanATick);

        Assert.Equal(whenTheGroupOpened, TicksSoFar());
    }

    /// <summary>
    /// Nothing came back for somebody whose conversation is on this reader's own list. Sending them
    /// back without a word was indistinguishable from a click that did not register - the only sign
    /// anything had happened was a 404 in the browser's own console.
    ///
    /// Checked by hand until now, on the grounds that rendering this page under bUnit meant standing
    /// up seventeen injected services and the browser crypto behind them. It did; that is what the
    /// harness above is, and it costs one more test rather than a second one of those.
    /// </summary>
    [Fact]
    public void An_account_the_api_will_not_resolve_says_so_rather_than_opening_an_empty_thread()
    {
        _theOtherAccountResolves = false;
        JSInterop.SetupModule("./js/presence.js").Setup<bool>("isPageVisible", _ => true).SetResult(true);

        var cut = RenderComponent<Chat>(parameters => parameters.Add(page => page.UserId, OtherUserId));

        cut.WaitForAssertion(() => Assert.Contains(
            "Orbit can't reach that account", cut.Markup, StringComparison.Ordinal));
    }

    /// <summary>
    /// And it does not leave a loop running against the conversation it could not open. The page's
    /// load gives up before it starts polling, which is easy to undo by moving one line.
    /// </summary>
    [Fact]
    public async Task A_conversation_that_could_not_be_opened_is_not_polled()
    {
        _theOtherAccountResolves = false;
        JSInterop.SetupModule("./js/presence.js").Setup<bool>("isPageVisible", _ => true).SetResult(true);
        var cut = RenderComponent<Chat>(parameters => parameters.Add(page => page.UserId, OtherUserId));
        cut.WaitForAssertion(() => Assert.Contains("Orbit can't reach that account", cut.Markup, StringComparison.Ordinal));

        await Task.Delay(LongerThanATick);

        Assert.Equal(0, TicksSoFar());
    }

    /// <summary>
    /// "Read" is something somebody did, not something an open window claims. A window that is visible
    /// but not focused - a second screen, a chat left open behind the editor - and a tab behind others
    /// both mark nothing, however long they poll and whatever is on screen in them. The seen-state
    /// callback is fired too, as a scroll or a focus event would, so neither path can mark on its own.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_window_nobody_is_at_marks_nothing_read(bool isPageVisible)
    {
        _isPageVisible = isPageVisible;
        _isInFront = false;
        var message = Receive(minutesAgo: 1);
        _newestInView = message.Id;
        var cut = RenderTheConversation();

        await WaitUntilTheLoopHasTickedAsync(times: 3);
        await cut.InvokeAsync(() => cut.Instance.OnThreadSeenMayHaveChanged());

        Assert.Empty(ReadsSent());
    }

    /// <summary>
    /// Only up to the newest message that has been on screen. One that arrived below the bottom of the
    /// list has not been read because the conversation was open.
    /// </summary>
    [Fact]
    public async Task A_message_not_yet_in_view_is_not_marked_read()
    {
        var inView = Receive(minutesAgo: 2);
        Receive(minutesAgo: 1);
        _newestInView = inView.Id;
        RenderTheConversation();

        await WaitUntilAReadHasBeenSentAsync();
        await WaitUntilTheLoopHasTickedAsync(times: 3);

        Assert.All(ReadsSent(), readUpTo => Assert.Equal(inView.SentAtUtc, readUpTo));
    }

    /// <summary>
    /// Scrolling down to it is what marks it - straight away, from the scroll, rather than on whichever
    /// poll comes next, which behind a live connection is twenty seconds off.
    /// </summary>
    [Fact]
    public async Task Scrolling_a_message_into_view_marks_up_to_it()
    {
        var first = Receive(minutesAgo: 2);
        var second = Receive(minutesAgo: 1);
        _newestInView = first.Id;
        var cut = RenderTheConversation();
        await WaitUntilAReadHasBeenSentAsync();

        _newestInView = second.Id;
        await cut.InvokeAsync(() => cut.Instance.OnThreadSeenMayHaveChanged());

        Assert.Equal(second.SentAtUtc, ReadsSent()[^1]);
    }

    /// <summary>The other way in: coming back to a window that already has the message on screen.</summary>
    [Fact]
    public async Task Focusing_the_window_marks_what_is_already_in_view()
    {
        _isInFront = false;
        var message = Receive(minutesAgo: 1);
        _newestInView = message.Id;
        var cut = RenderTheConversation();
        await WaitUntilTheLoopHasTickedAsync(times: 1);
        Assert.Empty(ReadsSent());

        _isInFront = true;
        await cut.InvokeAsync(() => cut.Instance.OnThreadSeenMayHaveChanged());

        Assert.NotEmpty(ReadsSent());
        Assert.All(ReadsSent(), readUpTo => Assert.Equal(message.SentAtUtc, readUpTo));
    }

    /// <summary>
    /// The bell's entries about the conversation follow what was seen, not the open window. They used to
    /// be cleared as the conversation loaded - before anything in it was drawn - so a window on a second
    /// screen took the notification away from somebody who had not looked at the message yet.
    /// </summary>
    [Fact]
    public async Task A_window_nobody_is_at_leaves_the_conversations_notifications_alone()
    {
        _isInFront = false;
        var message = Receive(minutesAgo: 1);
        _newestInView = message.Id;
        var feed = TheBellHasNewsAboutThisConversation();
        var cut = RenderTheConversation();

        await WaitUntilTheLoopHasTickedAsync(times: 3);
        await cut.InvokeAsync(() => cut.Instance.OnThreadSeenMayHaveChanged());

        Assert.Equal(0, TimesAsked("api/notifications/read-at"));
        Assert.True(feed.HasUnreadFor($"/chat/{OtherUserId}"));
    }

    /// <summary>
    /// An entry says only that a message arrived, not which one - so while one of theirs is still below
    /// the bottom of the list, the entry stays, even though what is above it has been marked read.
    /// </summary>
    [Fact]
    public async Task A_notification_stays_while_their_newest_message_is_not_yet_in_view()
    {
        var inView = Receive(minutesAgo: 2);
        Receive(minutesAgo: 1);
        _newestInView = inView.Id;
        var feed = TheBellHasNewsAboutThisConversation();
        RenderTheConversation();

        await WaitUntilAReadHasBeenSentAsync();
        await WaitUntilTheLoopHasTickedAsync(times: 3);

        Assert.Equal(0, TimesAsked("api/notifications/read-at"));
        Assert.True(feed.HasUnreadFor($"/chat/{OtherUserId}"));
    }

    /// <summary>Scrolling their newest message into view is what clears it - from the scroll, not the next poll.</summary>
    [Fact]
    public async Task Scrolling_their_newest_message_into_view_clears_its_notification()
    {
        var first = Receive(minutesAgo: 2);
        var second = Receive(minutesAgo: 1);
        _newestInView = first.Id;
        var feed = TheBellHasNewsAboutThisConversation();
        var cut = RenderTheConversation();
        await WaitUntilAReadHasBeenSentAsync();
        Assert.True(feed.HasUnreadFor($"/chat/{OtherUserId}"));

        _newestInView = second.Id;
        await cut.InvokeAsync(() => cut.Instance.OnThreadSeenMayHaveChanged());

        Assert.True(TimesAsked("api/notifications/read-at") >= 1);
        Assert.False(feed.HasUnreadFor($"/chat/{OtherUserId}"));
    }

    /// <summary>An unread entry in the bell about this conversation, as a message from the other party records one.</summary>
    private NotificationFeedState TheBellHasNewsAboutThisConversation()
    {
        var feed = Services.GetRequiredService<NotificationFeedState>();
        feed.Set(
        [
            new NotificationEntryDto(
                Guid.NewGuid(), "ChatMessage", "New message", "New message from {0}", $"/chat/{OtherUserId}",
                TheStartOfTheTest, IsRead: false)
        ]);
        return feed;
    }

    /// <summary>
    /// A message from the other party, answered by the conversation read from now on and answerable by
    /// chatSeen.js as the one in view once a test says it is.
    /// </summary>
    private ChatMessageDto Receive(int minutesAgo)
    {
        var message = new ChatMessageDto(
            Guid.NewGuid(), OtherUserId, OwnUserId, "sealed", "nonce", TheStartOfTheTest.AddMinutes(-minutesAgo),
            IsEdited: false, EditedAtUtc: null);
        lock (_countingLock)
        {
            _conversation.Add(message);
        }

        _seenModule.Setup<string?>("newestInView", _ => _newestInView == message.Id).SetResult(message.Id.ToString());
        return message;
    }

    private IReadOnlyList<DateTimeOffset?> ReadsSent()
    {
        lock (_countingLock)
        {
            return [.. _readsSent];
        }
    }

    private async Task WaitUntilAReadHasBeenSentAsync()
    {
        var deadline = DateTime.UtcNow + EnoughForAFewTicks;
        while (ReadsSent().Count == 0)
        {
            Assert.True(DateTime.UtcNow < deadline, "The page never marked anything read.");
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }
    }

    /// <summary>
    /// What the live connection is for: an announcement is answered by reading the conversation at once,
    /// not at the next tick. Announced straight after the first load and looked for well inside the
    /// second the loop waits before its first tick, so a read that shows up can only be the answer.
    /// </summary>
    [Fact]
    public async Task An_announcement_reads_the_conversation_at_once()
    {
        RenderTheConversation();
        var afterTheFirstLoad = TimesAsked("api/chat/messages");

        _liveUpdates.Announce(LiveUpdateMessages.ChatChanged);

        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(700);
        while (TimesAsked("api/chat/messages") == afterTheFirstLoad)
        {
            Assert.True(
                DateTime.UtcNow < deadline,
                "The announcement was not answered before the loop's first tick could have come.");
            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }
    }

    /// <summary>
    /// The rule the timer follows holds for an announcement too: nothing is read behind other tabs. The
    /// page still hears it - it asks whether it is in front of somebody - and then fetches nothing.
    /// </summary>
    [Fact]
    public async Task An_announcement_reads_nothing_while_the_tab_is_behind_other_tabs()
    {
        _isPageVisible = false;
        RenderTheConversation();
        var afterTheFirstLoad = TimesAsked("api/chat/messages");
        var askedBefore = TicksSoFar();

        _liveUpdates.Announce(LiveUpdateMessages.ChatChanged);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        Assert.True(TicksSoFar() > askedBefore, "The page never heard the announcement.");
        Assert.Equal(afterTheFirstLoad, TimesAsked("api/chat/messages"));
    }

    private IRenderedComponent<Chat> RenderTheConversation()
    {
        // Set here rather than in the constructor: a planned invocation answers with one result, and
        // what that result is, is the one thing these tests vary.
        JSInterop.SetupModule("./js/presence.js")
            .Setup<bool>("isPageVisible", _ => true)
            .SetResult(_isPageVisible);

        var cut = RenderComponent<Chat>(parameters => parameters.Add(page => page.UserId, OtherUserId));
        // The loop only starts once the first load has finished, and that load is asynchronous - so a
        // test that began counting immediately would be racing it.
        cut.WaitForAssertion(() => Assert.True(TimesAsked("api/chat/messages") > 0));
        return cut;
    }

    private int TimesAsked(string path)
    {
        lock (_countingLock)
        {
            return _requestsByPath.GetValueOrDefault(path);
        }
    }

    /// <summary>
    /// How many times the loop has come round. Counted off the one thing it does before deciding
    /// anything else - asking whether the tab is in front of somebody - so it counts a tick that went
    /// on to fetch nothing just the same.
    /// </summary>
    private int TicksSoFar()
        => JSInterop.Invocations.Count(invocation => invocation.Identifier == "isPageVisible");

    /// <summary>
    /// The tick count once a tick already under way has finished recording itself. A loop is stopped
    /// between ticks, so one can be mid-flight when the page is left - counting before it lands would
    /// read the stop as a tick that came afterwards.
    /// </summary>
    private async Task<int> TicksOnceTheLastOneHasLandedAsync()
    {
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        return TicksSoFar();
    }

    /// <summary>
    /// Waits for the loop rather than for the clock, and does its own waiting rather than using
    /// bUnit's: <c>WaitForAssertion</c> re-checks when the component renders, and a tick behind a
    /// hidden tab renders nothing at all - which is precisely the case these tests exist for.
    /// </summary>
    private async Task WaitUntilTheLoopHasTickedAsync(int times)
    {
        var deadline = DateTime.UtcNow + EnoughForAFewTicks;
        while (TicksSoFar() < times)
        {
            Assert.True(
                DateTime.UtcNow < deadline,
                $"The poll loop came round {TicksSoFar()} times, not the {times} this test needs.");
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }
    }

    /// <summary>
    /// One stub behind every client this page injects, answering the emptiest thing each endpoint can
    /// say and counting what was asked for. Empty on purpose: these tests are about how often the page
    /// asks, not about what comes back.
    /// </summary>
    private void RegisterTheApi(OrbitAuthenticationStateProvider authenticationStateProvider)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(Answer))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var chatApiClient = new ChatApiClient(httpClient);
        var usersApiClient = new UsersApiClient(httpClient);
        var ownKey = new OwnEncryptionKeyProvider(
            JSInterop.JSRuntime, usersApiClient, authenticationStateProvider);
        Services.AddSingleton(chatApiClient);
        Services.AddSingleton(usersApiClient);
        Services.AddSingleton(ownKey);
        Services.AddSingleton(new EncryptedChatMessageSender(
            JSInterop.JSRuntime, ownKey, usersApiClient, chatApiClient));
        Services.AddSingleton(new NotificationsApiClient(httpClient));
        Services.AddSingleton(new CalendarApiClient(httpClient));
        Services.AddSingleton(new NotesApiClient(httpClient));
        Services.AddSingleton(new TasksApiClient(httpClient));
        Services.AddSingleton(new InventoryApiClient(httpClient));
        Services.AddSingleton(new PlacesApiClient(httpClient));
        Services.AddSingleton(new AuthApiClient(httpClient, new TokenStore(new StubJSRuntime())));

        // Never connected, which is the shape every one of these tests wants: the loop then runs at its
        // one-second pace rather than the twenty-second one it drops to behind a live connection.
        _liveUpdates = new LiveUpdatesConnection(
            new TokenStore(new StubJSRuntime()),
            new TokenRefreshService(new TokenStore(new StubJSRuntime()), httpClient),
            "https://example.test/",
            NullLogger<LiveUpdatesConnection>.Instance);
        Services.AddSingleton(_liveUpdates);

        Services.AddSingleton(new PanelPreferences(new StubJSRuntime()));
        Services.AddSingleton(new PageVisibility(JSInterop.JSRuntime));
        Services.AddSingleton(new ChatSeenProbe(JSInterop.JSRuntime));
    }

    private HttpResponseMessage Answer(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        var withoutQuery = path;
        lock (_countingLock)
        {
            // Counted by the endpoint rather than by the whole address: the conversation read carries
            // a "since" parameter that changes as messages arrive, and every tick would be its own key.
            var key = Endpoint(withoutQuery);
            _requestsByPath[key] = _requestsByPath.GetValueOrDefault(key) + 1;
        }

        return withoutQuery switch
        {
            "api/users/me/permissions" => Json($"{{\"granted\":[\"{nameof(ApplicationPermission.Chat)}\"]}}"),
            "api/chat/contacts" or "api/chat/groups" => Json("[]"),
            var users when users == $"api/users/{OtherUserId}" => _theOtherAccountResolves
                ? Json(JsonSerializer.Serialize(new
                {
                    id = OtherUserId,
                    userName = "anna",
                    displayName = "Anna Kowalska",
                    publicKeyBase64 = "a-key"
                }))
                : new HttpResponseMessage(HttpStatusCode.NotFound),
            var access when access.StartsWith("api/chat/conversations", StringComparison.Ordinal)
                => new HttpResponseMessage(HttpStatusCode.NoContent),
            var receipt when receipt.EndsWith("/read-receipt", StringComparison.Ordinal)
                => Json("{\"readUpToUtc\":null}"),
            var read when read.EndsWith("/read", StringComparison.Ordinal) => RecordTheRead(request),
            var conversation when conversation == $"api/chat/messages/{OtherUserId}"
                => Json(JsonSerializer.Serialize(TheConversation(), new JsonSerializerOptions(JsonSerializerDefaults.Web))),
            var messages when messages.StartsWith("api/chat/messages", StringComparison.Ordinal) => Json("[]"),
            _ => Json("[]")
        };
    }

    private HttpResponseMessage RecordTheRead(HttpRequestMessage request)
    {
        var readUpToUtc = HttpUtility.ParseQueryString(request.RequestUri!.Query)["readUpToUtc"];
        lock (_countingLock)
        {
            _readsSent.Add(readUpToUtc is null
                ? null
                : DateTimeOffset.Parse(readUpToUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        }

        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private IReadOnlyList<ChatMessageDto> TheConversation()
    {
        lock (_countingLock)
        {
            return [.. _conversation];
        }
    }

    /// <summary>
    /// Which endpoint a path belongs to, for counting. The conversation read and the read mark share a
    /// prefix and are told apart by their tail, which is the only distinction these tests need.
    /// </summary>
    private static string Endpoint(string path)
        => path.EndsWith("/read", StringComparison.Ordinal) ? "api/chat/messages/read"
            : path.EndsWith("/read-receipt", StringComparison.Ordinal) ? "api/chat/messages/read-receipt"
            : path.StartsWith("api/chat/messages", StringComparison.Ordinal) ? "api/chat/messages"
            : path.StartsWith("api/chat/conversations", StringComparison.Ordinal) ? "api/chat/conversations"
            : path.StartsWith("api/users/", StringComparison.Ordinal) && path != "api/users/me/permissions"
                ? "api/users"
                : path;

    /// <summary>
    /// The page imports ./js/e2eeChat.js before it will load anything at all, and calls the scroll
    /// helper on every render. Answered rather than made loose, so a call nobody expected is still an
    /// error - what the sealing actually does is checked in a real browser by
    /// ci/verify-browser-crypto.mjs.
    /// </summary>
    private void TheBrowserCanSealAndOpen()
    {
        var crypto = JSInterop.SetupModule("./js/e2eeChat.js");
        crypto.Setup<bool>("hasOwnPrivateKey", _ => true).SetResult(true);
        crypto.Setup<string>("ensureOwnPublicKey", _ => true).SetResult("a-public-key");
        crypto.Setup<EncryptedChatMessageSender.EncryptedPayload>("encryptMessage", _ => true)
            .SetResult(new EncryptedChatMessageSender.EncryptedPayload("sealed", "nonce"));
        crypto.Setup<string?>("decryptMessage", _ => true).SetResult((string?)null);

        JSInterop.SetupVoid("OrbitChatScroll.observeScroll", _ => true).SetVoidResult();
        JSInterop.SetupVoid("OrbitChatScroll.unobserveScroll", _ => true).SetVoidResult();
        JSInterop.SetupVoid("OrbitChatScroll.scrollToBottom", _ => true).SetVoidResult();
        JSInterop.SetupVoid("OrbitChatScroll.scrollToMessage", _ => true).SetVoidResult();
        JSInterop.Setup<bool>("OrbitChatScroll.isScrolledNearBottom", _ => true).SetResult(true);

        // What is in front of the reader. Answered by matchers over the test's own fields rather than by
        // one result each, so a test can change its answer part-way - scroll, or give the window focus.
        _seenModule = JSInterop.SetupModule("./js/chatSeen.js");
        _seenModule.Setup<bool>("isInFront", _ => _isInFront).SetResult(true);
        _seenModule.Setup<bool>("isInFront", _ => !_isInFront).SetResult(false);
        _seenModule.Setup<string?>("newestInView", _ => _newestInView is null).SetResult((string?)null);
        _seenModule.SetupVoid("observe", _ => true).SetVoidResult();
        _seenModule.SetupVoid("unobserve", _ => true).SetVoidResult();
    }

    private OrbitAuthenticationStateProvider RegisterAuthentication()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        tokenStore.SetTokenAsync(AnUnsignedTokenFor(OwnUserId)).GetAwaiter().GetResult();
        var refreshHttpClient = new HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var authenticationStateProvider = new OrbitAuthenticationStateProvider(
            tokenStore, new TokenRefreshService(tokenStore, refreshHttpClient));
        Services.AddSingleton(authenticationStateProvider);
        Services.AddSingleton<AuthenticationStateProvider>(authenticationStateProvider);
        Services.AddAuthorizationCore();
        return authenticationStateProvider;
    }

    private void RegisterPermissions()
    {
        var permissions = new UserPermissionState(new UsersApiClient(
            new HttpClient(new StubHttpMessageHandler(
                _ => Json($"{{\"granted\":[\"{nameof(ApplicationPermission.Chat)}\"]}}")))
            {
                BaseAddress = new Uri("https://example.test/")
            }));
        permissions.EnsureLoadedAsync().GetAwaiter().GetResult();
        Services.AddSingleton(permissions);
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string AnUnsignedTokenFor(Guid userId)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(
            $$"""{"sub":"{{userId}}","email":"owner@example.com","name":"Test Owner"}"""));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
