using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts;
using Orbit.Core.Abstractions;
using Orbit.Contracts.Tasks;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// A private task list stores no item rows on the server at all - its entries live inside the sealed
/// payload - so the only thing that can give them an identity is the browser that seals them. It gave
/// them all the same one: <c>Guid.Empty</c>, for every entry of every private list.
///
/// That was invisible until an entry got a page addressed by its id. Then pressing the third entry of a
/// private list opened the first, because that is what a lookup by id finds when they all share one -
/// and the same address is built by the calendar and the dashboard, so all three were wrong together.
///
/// These run the real <see cref="PrivateContentSealer"/> over a stubbed <c>e2eeChat.js</c> that stores
/// the plain text rather than encrypting it: what is under test is which ids come back, not the crypto,
/// which is checked in a real browser by ci/verify-browser-crypto.mjs.
/// </summary>
public sealed class PrivateTaskListItemIdTests : OrbitTestContext
{
    private static readonly Guid OwnUserId = Guid.NewGuid();
    private static readonly Guid TaskListId = Guid.NewGuid();

    [Fact]
    public async Task Every_entry_of_a_private_list_comes_back_with_an_id_of_its_own()
    {
        var client = ClientThatSealsAndOpens();

        await client.CreateTaskListAsync(
            new CreateTaskRequest("Bank things", [Entry("Change the card"), Entry("Cancel the standing order")],
                IsGroup: false, IsPrivate: true, EncryptedContent: null));
        var opened = await client.GetTaskListByIdAsync(TaskListId);

        var ids = opened!.Items.Select(item => item.Id).ToList();
        Assert.DoesNotContain(Guid.Empty, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    /// <summary>
    /// An entry that already has an id keeps it, which is what a save is for: an address somebody has
    /// open, or has come back to, still names the same entry afterwards. Minting a fresh one each save
    /// would have been the easy answer and the wrong one.
    /// </summary>
    [Fact]
    public async Task An_entry_that_already_had_an_id_keeps_it_through_a_save()
    {
        var client = ClientThatSealsAndOpens();
        var existingId = Guid.NewGuid();

        await client.CreateTaskListAsync(
            new CreateTaskRequest("Bank things", [Entry("Change the card") with { Id = existingId }],
                IsGroup: false, IsPrivate: true, EncryptedContent: null));
        var opened = await client.GetTaskListByIdAsync(TaskListId);

        Assert.Equal(existingId, Assert.Single(opened!.Items).Id);
    }

    /// <summary>
    /// And a list sealed before any of this still reads: every entry carries Guid.Empty in there, and
    /// each is given one derived from the list and its position on the way out - the same id on the next
    /// read and after a reload, because an address that changed each time would be worse than none.
    /// </summary>
    [Fact]
    public async Task A_list_sealed_before_ids_were_kept_is_given_stable_ones()
    {
        var client = ClientThatSealsAndOpens();
        TheSealedPayloadIs(JsonSerializer.Serialize(new SealedTaskList(
            "Bank things",
            [SealedEntry("Change the card"), SealedEntry("Cancel the standing order")])));

        var firstRead = await client.GetTaskListByIdAsync(TaskListId);
        var secondRead = await client.GetTaskListByIdAsync(TaskListId);

        var ids = firstRead!.Items.Select(item => item.Id).ToList();
        Assert.DoesNotContain(Guid.Empty, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(ids, secondRead!.Items.Select(item => item.Id));
    }

    /// <summary>
    /// Everything an entry says comes back out of the seal, not only what entries said when private lists
    /// were written. The sealed entry was built by hand from those nine fields, so a private list saved
    /// in a browser came back with every entry a plain, unfiled, uncoloured checklist line - its place,
    /// categories, description and priority gone.
    /// </summary>
    [Fact]
    public async Task Everything_an_entry_says_survives_being_sealed()
    {
        var client = ClientThatSealsAndOpens();

        await client.CreateTaskListAsync(
            new CreateTaskRequest(
                "Bank things",
                [
                    Entry("Visit the branch") with
                    {
                        Kind = "Location",
                        Location = "Długa 4",
                        Categories = ["bank"],
                        Notes = "Bring the passport",
                        Priority = "High",
                        Colour = "#aa3355"
                    }
                ],
                IsGroup: false, IsPrivate: true, EncryptedContent: null));
        var entry = Assert.Single((await client.GetTaskListByIdAsync(TaskListId))!.Items);

        Assert.Equal("Location", entry.Kind);
        Assert.Equal("Długa 4", entry.Location);
        Assert.Equal(["bank"], entry.AllCategories);
        Assert.Equal("Bring the passport", entry.Notes);
        Assert.Equal("High", entry.Priority);
        Assert.Equal("#aa3355", entry.Colour);
    }

    /// <summary>
    /// Ticking an entry of a private list keeps it private, at its priority, with its tags. The tick used
    /// to send the list back with nothing but its title, entries and group flag - which the update endpoint
    /// reads as "not private, Normal priority" - so the moment an entry on a private list was ticked in a
    /// browser, the list was saved in the clear, entries and all, at Normal.
    /// </summary>
    [Fact]
    public async Task Ticking_an_entry_of_a_private_list_keeps_it_private_with_its_priority_and_tags()
    {
        // The tick also asks the page things this fixture has no page for; only the sealing is under test.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var client = ClientThatSealsAndOpens();
        await client.CreateTaskListAsync(
            new CreateTaskRequest("Bank things", [Entry("Change the card") with { Id = Guid.NewGuid() }],
                IsGroup: false, IsPrivate: true, EncryptedContent: null, Priority: "High", Tags: ["bank"]));
        // The server holds the priority readably, beside the seal - which this stub does not keep.
        var opened = (await client.GetTaskListByIdAsync(TaskListId))! with { Priority = "High" };
        var ticking = new TaskItemCompletion(
            client, Services.GetRequiredService<NavigationManager>(), JSInterop.JSRuntime,
            new Translations(new StubJSRuntime()), NullLogger<TaskItemCompletion>.Instance);

        await ticking.TickAsync(opened, opened.Items[0], TickState.Completed);

        var sent = Assert.Single(_updates);
        Assert.True(sent.IsPrivate);
        Assert.Equal("High", sent.Priority);
        // Nothing readable left the browser: the entries went into the seal, and so did the tag.
        Assert.Empty(sent.Items);
        Assert.Equal(string.Empty, sent.Title);
        var resealed = JsonSerializer.Deserialize<SealedTaskList>(
            (string)JSInterop.Invocations["encryptForSelf"].Last().Arguments[1]!)!;
        Assert.Equal(["bank"], resealed.Tags);
        Assert.True(Assert.Single(resealed.Items).IsCompleted);
    }

    /// <summary>Every update the client sent, as the server would have read it.</summary>
    private readonly List<UpdateTaskRequest> _updates = [];

    private static TaskItemRequest Entry(string description)
        => new(description, Id: null, DueDateUtc: null, IsCompleted: false, LinkedTaskListId: null,
            OverdueNotificationChannel: "None", RemindDaily: false, DailyReminderNotificationChannel: "None",
            DailyReminderTimeOfDay: default);

    /// <summary>An entry as everything sealed before 2026-09-07 holds one: no id at all.</summary>
    private static TaskItemDto SealedEntry(string description)
        => new(Guid.Empty, description, DueDateUtc: null, IsCompleted: false, LinkedTaskListId: null,
            OverdueNotificationChannel: "None", RemindDaily: false, DailyReminderNotificationChannel: "None",
            DailyReminderTimeOfDay: default);

    /// <summary>
    /// A client whose sealer runs for real over a stubbed module: "encryptForSelf" keeps the plain text
    /// and "decryptForSelf" hands back whatever was kept, so a create followed by a read is a genuine
    /// round trip through the same code the browser runs.
    /// </summary>
    private TasksApiClient ClientThatSealsAndOpens()
    {
        var crypto = JSInterop.SetupModule("./js/e2eeChat.js");
        crypto.Setup<bool>("hasOwnPrivateKey", _ => true).SetResult(true);
        crypto.Setup<string>("ensureOwnPublicKey", _ => true).SetResult("a-public-key");
        crypto.Setup<PrivateContentSealer.SealedContent>("encryptForSelf", _ => true)
            .SetResult(new PrivateContentSealer.SealedContent("sealed", "nonce"));
        // "decryptForSelf" is planned again each time the payload changes - see TheSealedPayloadIs.

        var tokenStore = new TokenStore(new StubJSRuntime());
        tokenStore.SetTokenAsync(AnUnsignedTokenFor(OwnUserId)).GetAwaiter().GetResult();
        var refreshClient = new HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var authenticationStateProvider = new OrbitAuthenticationStateProvider(
            tokenStore, new TokenRefreshService(tokenStore, refreshClient));

        var httpClient = new HttpClient(new StubHttpMessageHandler(Answer))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var sealer = new PrivateContentSealer(
            new OwnEncryptionKeyProvider(JSInterop.JSRuntime, new UsersApiClient(httpClient), authenticationStateProvider),
            authenticationStateProvider,
            JSInterop.JSRuntime);

        return new TasksApiClient(httpClient, privateContentSealer: sealer);
    }

    /// <summary>
    /// Stands in for the API: a create keeps what was sealed, and the read hands back a list carrying
    /// exactly what the server would - the ciphertext, an empty title and no items at all.
    /// </summary>
    private HttpResponseMessage Answer(HttpRequestMessage request)
    {
        // A list's own update - not the key provider's, which puts this browser's public key first.
        if (request.Method == HttpMethod.Put && request.RequestUri!.AbsolutePath.Contains("/api/tasks/"))
        {
            _updates.Add(JsonSerializer.Deserialize<UpdateTaskRequest>(
                request.Content!.ReadAsStringAsync().GetAwaiter().GetResult(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
            TheSealedPayloadIs((string)JSInterop.Invocations["encryptForSelf"].Last().Arguments[1]!);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (request.Method == HttpMethod.Post)
        {
            // Whatever the client sealed is what "encryptForSelf" was handed - read it back off the
            // stub's own record of the call rather than off the wire, which carries only ciphertext.
            TheSealedPayloadIs((string)JSInterop.Invocations["encryptForSelf"].Last().Arguments[1]!);
            return Json(JsonSerializer.Serialize(TaskListId));
        }

        return Json(JsonSerializer.Serialize(new TaskDto(
            TaskListId, string.Empty, [], IsCompleted: false, IsGroup: false, IsPrivate: true,
            new EncryptedContentDto("sealed", "nonce"), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null)));
    }

    /// <summary>
    /// What the next "decryptForSelf" answers with. Planned afresh rather than held onto: bUnit uses the
    /// most recently registered handler that matches, and naming the planned-invocation type to keep one
    /// around buys nothing here.
    /// </summary>
    private void TheSealedPayloadIs(string plainText)
    {
        var crypto = JSInterop.SetupModule("./js/e2eeChat.js");
        crypto.Setup<string?>("decryptForSelf", _ => true).SetResult(plainText);
        // Planned again with it: setting the module up afresh is what makes the new answer win, and it
        // leaves the sealing half unanswered - which a test that seals twice (create, then tick) needs.
        crypto.Setup<bool>("hasOwnPrivateKey", _ => true).SetResult(true);
        crypto.Setup<string>("ensureOwnPublicKey", _ => true).SetResult("a-public-key");
        crypto.Setup<PrivateContentSealer.SealedContent>("encryptForSelf", _ => true)
            .SetResult(new PrivateContentSealer.SealedContent("sealed", "nonce"));
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string AnUnsignedTokenFor(Guid userId)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes($$"""{"sub":"{{userId}}"}"""));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
