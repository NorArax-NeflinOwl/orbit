using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Inventory;
using Orbit.Contracts.Notifications;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using System.Text.Json;
using Orbit.Contracts.Notes;
using Orbit.Web.Components;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// What a warehouse says about itself beyond its name, and whether that survives a save. A field the
/// form shows but does not send back is the worst of both: it looks saved and is gone on the next load.
/// The same class of bug has bitten this app three times, always the same way - the save built a fresh
/// object and forgot a field - so it is pinned here rather than assumed.
/// </summary>
public sealed class WarehouseDescriptionTests : OrbitTestContext
{
    private static readonly Guid WarehouseId = Guid.NewGuid();

    private string? _lastSavedJson;

    public WarehouseDescriptionTests() => Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    /// <summary>
    /// The name and what is under it, as the one field they are written in holds them - see
    /// TitledDescription. Read through the surface rather than off the DOM: it is contenteditable driven
    /// from JS, which a test renderer has none of.
    /// </summary>
    private static string[] WhatTheFieldHolds(IRenderedFragment cut)
        => [.. cut.FindComponent<ChecklistTextEditor>().Instance.Lines.Select(line => line.Text)];

    /// <summary>What that field reports after somebody types into it, called the way its own JS calls it.</summary>
    private static void WriteIntoTheField(IRenderedFragment cut, params string[] lines)
    {
        var editor = cut.FindComponent<ChecklistTextEditor>().Instance;
        var written = lines.Select(line => new NoteContentLineDto(line, IsChecklistItem: false, IsChecked: false));
        cut.InvokeAsync(() => editor.OnLinesChangedFromJs(
            JsonSerializer.Serialize(written, new JsonSerializerOptions(JsonSerializerDefaults.Web))))
            .GetAwaiter().GetResult();
    }


    [Fact]
    public void What_the_warehouse_is_for_is_shown_under_its_name()
    {
        RegisterApiClients(description: "Everything that lives in the cellar");

        var cut = Render();

        Assert.Equal(["Pantry", "Everything that lives in the cellar"], WhatTheFieldHolds(cut));
    }

    [Fact]
    public void And_goes_back_with_the_save()
    {
        RegisterApiClients(description: "Everything that lives in the cellar");
        var cut = Render();

        WriteIntoTheField(cut, "Pantry", "The cellar, and the shelf by the door");
        ClickButtonSaying(cut, "Save");

        Assert.NotNull(_lastSavedJson);
        Assert.Contains("The cellar, and the shelf by the door", _lastSavedJson);
    }

    /// <summary>
    /// An item marked as looked at every round has to come back marked, or the next save turns it off -
    /// the save sends the whole item list, so a flag the form dropped is a flag the server clears.
    /// </summary>
    [Fact]
    public void An_item_checked_every_round_stays_checked_across_a_save()
    {
        RegisterApiClients(description: "", items: [Item("Milk", quantity: 10, isCheckedRegularly: true)]);
        var cut = Render();

        ClickButtonSaying(cut, "Save");

        Assert.NotNull(_lastSavedJson);
        Assert.Contains("\"isCheckedRegularly\":true", _lastSavedJson);
    }

    private IRenderedComponent<WarehouseEditor> Render()
        => RenderComponent<WarehouseEditor>(parameters => parameters.Add(editor => editor.WarehouseId, WarehouseId));

    private static void ClickButtonSaying(IRenderedFragment cut, string label)
        => ButtonSaying(cut, label).Click();

    /// <summary>
    /// A button by what it says - its words, or the name it carries for a screen reader, since an
    /// editor's Save and Cancel are icons now (see EditorActions.razor).
    /// </summary>
    private static AngleSharp.Dom.IElement ButtonSaying(IRenderedFragment cut, string label)
        => cut.FindAll("button").First(button =>
            button.TextContent.Contains(label, StringComparison.Ordinal)
                || string.Equals(button.GetAttribute("aria-label"), label, StringComparison.Ordinal));

    private void RegisterApiClients(string description, IReadOnlyList<InventoryItemDto>? items = null)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Put && path.EndsWith($"/{WarehouseId}", StringComparison.Ordinal))
            {
                _lastSavedJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.EndsWith("/settings", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new NotificationSettingsDto(
                        true, true, true, true, ShowExceptionDetails: false,
                        BannerVisibleSeconds: 5, BannerMinimumGapSeconds: 5))
                };
            }

            if (path.EndsWith("/items", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(items ?? []) };
            }

            if (path.EndsWith("/lock", StringComparison.Ordinal) || path.StartsWith("/api/share-links", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new WarehouseDto(
                    WarehouseId, "Pantry", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                    IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit",
                    LockedByUserName: null, OriginalOwnerUserId: null, Description: description))
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new InventoryApiClient(httpClient));
        Services.AddSingleton(new NotificationsApiClient(httpClient));
        Services.AddSingleton(new PublicShareApiClient(httpClient));
    }

    private static InventoryItemDto Item(string name, decimal quantity, bool isCheckedRegularly)
        => new(
            Guid.NewGuid(), name, "Food", "Dairy", quantity, MinimumQuantity: null, Unit: "Piece",
            ExpiryDate: null, ExpiryNotificationChannel: "None", IsBelowMinimum: false,
            HasPendingRestockTask: false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsCheckedRegularly: isCheckedRegularly);
}
