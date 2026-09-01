using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Notifications;
using Orbit.Contracts.Tasks;
using Orbit.Core.Tasks;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// What an entry's form offers, which now depends on what the entry is. The row itself reports - what it
/// says, when it is due, what kind it is - and everything editable waits behind the toggle, because a
/// list of thirty items was thirty rows of boxes. The list's own title and description are here too:
/// they sit at the top of the same form.
/// </summary>
public sealed class TaskEditorItemFormTests : OrbitTestContext
{
    private static readonly Guid TaskListId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();

    public TaskEditorItemFormTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterAuthentication();
        RegisterPermissions();
    }

    [Fact]
    public void The_row_itself_carries_nothing_to_type_into()
    {
        RegisterApiClients(AnItem());

        var cut = Render();

        // The date, the time and Remove all moved behind the toggle. What is left is what somebody
        // reads down a list for.
        var row = cut.Find(".editor-item-summary");
        Assert.Empty(row.QuerySelectorAll("input[type=date]"));
        Assert.Empty(row.QuerySelectorAll("input[type=time]"));
        Assert.DoesNotContain("Remove", row.TextContent);
    }

    [Fact]
    public void A_dated_entry_still_says_so_on_the_row()
    {
        // Hiding the boxes must not hide the fact. Opening every item to find the one with a date on it
        // would be a worse list than the one full of boxes.
        RegisterApiClients(AnItem(dueDateUtc: new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero)));

        var cut = Render();

        Assert.Contains("14.09.2026", cut.Find(".editor-item-summary").TextContent);
    }

    [Fact]
    public void Opening_an_entry_offers_the_four_fields_every_entry_has()
    {
        RegisterApiClients(AnItem());
        var cut = Render();

        ExpandTheOnlyItem(cut);

        var details = cut.Find(".editor-item-details").TextContent;
        Assert.Contains("Type", details);
        Assert.Contains("Due date", details);
        Assert.Contains("Due time", details);
        Assert.Contains("Remove", details);
    }

    [Fact]
    public void A_checklist_entry_keeps_the_fields_a_checklist_entry_had()
    {
        RegisterApiClients(AnItem());
        var cut = Render();

        ExpandTheOnlyItem(cut);

        var details = cut.Find(".editor-item-details").TextContent;
        Assert.Contains("Stands for these lists", details);
        Assert.Contains("Overdue notification", details);
        Assert.Contains("Remind daily", details);
    }

    [Fact]
    public void An_inventory_entry_is_asked_nothing_a_checklist_entry_is_asked()
    {
        // Its fields are the shelf item's - see TaskEditor's Inventory branch. Offering "Link to list"
        // beside them would be offering something that means nothing for this kind.
        RegisterApiClients(AnItem(kind: nameof(TaskItemKind.Inventory)));
        var cut = Render();

        ExpandTheOnlyItem(cut);

        Assert.DoesNotContain("Link to list", cut.Find(".editor-item-details").TextContent);
    }

    [Fact]
    public void A_daily_reminder_with_no_hour_is_refused_rather_than_sent_at_midnight()
    {
        RegisterApiClients(AnItem(remindDaily: true));
        var cut = Render();

        ClickButtonSaying(cut, "Save");

        // An hour nobody chose is worse than being asked for one.
        Assert.Contains("needs a time", cut.Markup);
        Assert.Null(_lastSavedJson);
    }

    [Fact]
    public void A_daily_reminder_with_an_hour_saves()
    {
        RegisterApiClients(AnItem(remindDaily: true, dailyReminderTimeOfDay: new TimeOnly(7, 30)));
        var cut = Render();

        ClickButtonSaying(cut, "Save");

        Assert.NotNull(_lastSavedJson);
    }

    [Fact]
    public void What_the_list_is_for_is_shown_under_its_title()
    {
        RegisterApiClients(AnItem());

        var cut = Render();

        Assert.Equal("Errands", cut.Find(".titled-description-title").GetAttribute("value"));
        Assert.Equal("Things to pick up on the way home", cut.Find(".titled-description-body").GetAttribute("value"));
    }

    /// <summary>
    /// A field the form shows but does not send back looks saved and is gone on the next load. The save
    /// builds a fresh request object, which is exactly where this app has lost fields before.
    /// </summary>
    [Fact]
    public void And_goes_back_with_the_save()
    {
        RegisterApiClients(AnItem());
        var cut = Render();

        cut.Find(".titled-description-body").Input("Only what the shop is out of");
        ClickButtonSaying(cut, "Save");

        Assert.NotNull(_lastSavedJson);
        Assert.Contains("Only what the shop is out of", _lastSavedJson);
    }


    /// <summary>
    /// An entry can be pointed at several lists, and every one of them has to reach the save. The
    /// picker adds one at a time and the chosen ones are listed underneath, so the reader can see what
    /// the entry stands for without opening a dropdown.
    /// </summary>
    [Fact]
    public void An_entry_can_be_made_to_stand_for_two_lists_and_both_are_saved()
    {
        RegisterApiClients(AnItem());
        var cut = Render();
        ExpandTheOnlyItem(cut);

        var picker = cut.FindAll("select").Single(box => box.GetAttribute("aria-label") == "Stands for these lists");
        var choices = picker.QuerySelectorAll("option")
            .Select(option => option.GetAttribute("value"))
            .Where(value => !string.IsNullOrEmpty(value))
            .Take(2)
            .ToList();
        Assert.Equal(2, choices.Count);

        picker.Change(choices[0]);
        cut.FindAll("select").Single(box => box.GetAttribute("aria-label") == "Stands for these lists").Change(choices[1]);
        ClickButtonSaying(cut, "Save");

        Assert.NotNull(_lastSavedJson);
        Assert.Contains(choices[0]!, _lastSavedJson);
        Assert.Contains(choices[1]!, _lastSavedJson);
    }

    /// <summary>A list already named is not offered again - that would be offering to say it twice.</summary>
    [Fact]
    public void A_list_it_already_stands_for_is_not_offered_again()
    {
        RegisterApiClients(AnItem());
        var cut = Render();
        ExpandTheOnlyItem(cut);

        var picker = cut.FindAll("select").Single(box => box.GetAttribute("aria-label") == "Stands for these lists");
        var chosen = picker.QuerySelectorAll("option")
            .Select(option => option.GetAttribute("value"))
            .First(value => !string.IsNullOrEmpty(value));
        picker.Change(chosen);

        var offeredAfterwards = cut.FindAll("select")
            .Single(box => box.GetAttribute("aria-label") == "Stands for these lists")
            .QuerySelectorAll("option")
            .Select(option => option.GetAttribute("value"));
        Assert.DoesNotContain(chosen, offeredAfterwards);
    }

    private IRenderedComponent<TaskEditor> Render()
        => RenderComponent<TaskEditor>(parameters => parameters.Add(editor => editor.Id, TaskListId));

    private static void ExpandTheOnlyItem(IRenderedFragment cut) => cut.Find(".editor-item-toggle").Click();

    private static void ClickButtonSaying(IRenderedFragment cut, string label)
        => cut.FindAll("button").First(button => button.TextContent.Contains(label)).Click();

    private static TaskItemDto AnItem(
        string kind = nameof(TaskItemKind.Checklist), DateTimeOffset? dueDateUtc = null, bool remindDaily = false,
        TimeOnly dailyReminderTimeOfDay = default)
        => new(
            ItemId, "Buy milk", dueDateUtc, IsCompleted: false, LinkedTaskListId: null,
            OverdueNotificationChannel: "None", remindDaily, DailyReminderNotificationChannel: "Push",
            dailyReminderTimeOfDay, kind);

    private string? _lastSavedJson;

    private void RegisterApiClients(TaskItemDto item)
    {
        var taskList = new TaskDto(
            TaskListId, "Errands", [item], IsCompleted: false, IsGroup: false, IsPrivate: false,
            EncryptedContent: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null,
            Description: "Things to pick up on the way home");

        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Put && path.EndsWith($"/{TaskListId}", StringComparison.Ordinal))
            {
                _lastSavedJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.Contains("/notifications", StringComparison.Ordinal))
            {
                return Json(new NotificationSettingsDto(
                    true, true, true, true, ShowExceptionDetails: false,
                    BannerVisibleSeconds: 5, BannerMinimumGapSeconds: 5));
            }

            // The references route answers what shelf items this list's errands are about; these lists
            // carry none. Everything else that is asked for here is a collection nobody asserts on.
            if (path.EndsWith("/inventory-references", StringComparison.Ordinal)
                || path.Contains("/calendar", StringComparison.Ordinal)
                || path.Contains("/chat", StringComparison.Ordinal))
            {
                return Json(Array.Empty<object>());
            }

            if (path.StartsWith("/api/share-links", StringComparison.Ordinal) || path.EndsWith("/lock", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            // Two other lists as well, so the "stands for these lists" picker has something to offer -
            // it never offers the list being edited, which would be a link to itself.
            return path.EndsWith($"/{TaskListId}", StringComparison.Ordinal)
                ? JsonOf(taskList)
                : JsonOf(new[] { taskList, AnotherTaskList("Kitchen"), AnotherTaskList("Bathroom") });
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        Services.AddSingleton(new TasksApiClient(httpClient));
        Services.AddSingleton(new ChatApiClient(httpClient));
        Services.AddSingleton(new CalendarApiClient(httpClient));
        Services.AddSingleton(new NotificationsApiClient(httpClient));
        Services.AddSingleton(new PublicShareApiClient(httpClient));
        Services.AddSingleton(new InventoryApiClient(httpClient));
    }

    private static TaskDto AnotherTaskList(string title)
        => new(
            Guid.NewGuid(), title, [], IsCompleted: false, IsGroup: false, IsPrivate: false,
            EncryptedContent: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    private static HttpResponseMessage Json(object body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private static HttpResponseMessage JsonOf<T>(T body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private void RegisterAuthentication()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        tokenStore.SetTokenAsync(CreateUnsignedJwt()).GetAwaiter().GetResult();
        var refreshHttpClient = new HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var provider = new OrbitAuthenticationStateProvider(tokenStore, new TokenRefreshService(tokenStore, refreshHttpClient));
        Services.AddSingleton(provider);
        Services.AddSingleton<AuthenticationStateProvider>(provider);
        Services.AddAuthorizationCore();

        // The editor injects the chat sender for the sharing block, whether or not that block renders.
        var jsRuntime = JSInterop.JSRuntime;
        var usersApiClient = new UsersApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") });
        Services.AddSingleton(usersApiClient);
        Services.AddSingleton(new EncryptedChatMessageSender(
            jsRuntime,
            new OwnEncryptionKeyProvider(jsRuntime, usersApiClient, provider),
            usersApiClient,
            new ChatApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") })));
    }

    private void RegisterPermissions()
    {
        // Nothing granted: these tests are about an entry's own form, and the Sharing block below it
        // pulls in the chat stack, which has nothing to do with what is being asserted.
        var permissions = new UserPermissionState(new UsersApiClient(
            new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"granted\":[]}", Encoding.UTF8, "application/json")
            }))
            {
                BaseAddress = new Uri("https://example.test/")
            }));
        permissions.EnsureLoadedAsync().GetAwaiter().GetResult();
        Services.AddSingleton(permissions);
    }

    private static string CreateUnsignedJwt()
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(
            $$"""{"sub":"{{Guid.NewGuid()}}","email":"owner@example.com","name":"Test Owner"}"""));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
