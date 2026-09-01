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

    [Fact]
    public void What_the_warehouse_is_for_is_shown_under_its_name()
    {
        RegisterApiClients(description: "Everything that lives in the cellar");

        var cut = Render();

        Assert.Equal("Everything that lives in the cellar", cut.Find(".titled-description-body").GetAttribute("value"));
        Assert.Equal("Pantry", cut.Find(".titled-description-title").GetAttribute("value"));
    }

    [Fact]
    public void And_goes_back_with_the_save()
    {
        RegisterApiClients(description: "Everything that lives in the cellar");
        var cut = Render();

        cut.Find(".titled-description-body").Input("The cellar, and the shelf by the door");
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
        => cut.FindAll("button").First(button => button.TextContent.Contains(label)).Click();

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
