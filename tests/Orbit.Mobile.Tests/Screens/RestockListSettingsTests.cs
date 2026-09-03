using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Inventories;
using Orbit.Mobile.Api;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Inventory;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// How an inventory's restock list is built, and when it comes round. Orbit.Web has had these at the
/// bottom of its inventory editor all along; the phone could edit the shelf while the rule deciding what
/// that shelf asks for was reachable only from a browser.
///
/// They live on the server and nothing local stands in for them, so the awkward cases are the ones where
/// there is nothing to show.
/// </summary>
public sealed class RestockListSettingsTests
{
    [Fact]
    public async Task The_settings_the_server_holds_are_what_the_panel_shows()
    {
        using var context = new PanelContext();
        context.Server.RestockSettings = new RestockListSettingsDto(OnlyLinkedWithDueDate: true, new TimeOnly(7, 30));

        var panel = await context.OpenAsync(context.InventoryId);

        Assert.True(panel.IsOffered);
        Assert.True(panel.OnlyLinkedWithDueDate);
        Assert.Equal(new TimeSpan(7, 30, 0), panel.RefreshTime);
    }

    /// <summary>Saying which rule is on is not the same as saying what it does.</summary>
    [Fact]
    public async Task The_rule_is_described_in_words_and_follows_the_switch()
    {
        using var context = new PanelContext();
        var panel = await context.OpenAsync(context.InventoryId);

        var whenEverything = panel.RuleDescription;
        panel.OnlyLinkedWithDueDate = true;

        Assert.NotEqual(whenEverything, panel.RuleDescription);
    }

    [Fact]
    public async Task Saving_sends_the_settings_and_says_what_that_moved()
    {
        using var context = new PanelContext();
        context.Server.RestockRefresh = new RestockRefreshResultDto(AddedCount: 2, RemovedCount: 1);
        var panel = await context.OpenAsync(context.InventoryId);

        panel.OnlyLinkedWithDueDate = true;
        panel.RefreshTime = new TimeSpan(6, 15, 0);
        await panel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, context.Server.RestockSettingsSaved);
        Assert.Equal(new RestockListSettingsDto(true, new TimeOnly(6, 15)), context.Server.RestockSettings);
        Assert.True(panel.HasMessage);
    }

    [Fact]
    public async Task Refreshing_rebuilds_the_list_without_changing_the_settings()
    {
        using var context = new PanelContext();
        var panel = await context.OpenAsync(context.InventoryId);

        await panel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(1, context.Server.RestockRefreshesAsked);
        Assert.Equal(0, context.Server.RestockSettingsSaved);
    }

    /// <summary>
    /// An inventory the server has never seen has no list to build. Showing the defaults as though they
    /// were its settings would be inventing an answer.
    /// </summary>
    [Fact]
    public async Task A_inventory_the_server_has_never_seen_is_offered_nothing()
    {
        using var context = new PanelContext();

        Assert.False((await context.OpenAsync(inventoryServerId: null)).IsOffered);
    }

    /// <summary>Nothing local stands in for these, so with no connection the panel simply is not there.</summary>
    [Fact]
    public async Task With_no_connection_the_panel_is_not_there_rather_than_wrong()
    {
        using var context = new PanelContext();
        context.Server.IsUnreachable = true;

        Assert.False((await context.OpenAsync(context.InventoryId)).IsOffered);
    }

    /// <summary>A share can be read without carrying the settings behind it, which the API answers 404 to.</summary>
    [Fact]
    public async Task Settings_this_reader_may_not_see_are_offered_as_nothing()
    {
        using var context = new PanelContext();
        context.Server.RestockSettings = null;

        Assert.False((await context.OpenAsync(context.InventoryId)).IsOffered);
    }

    [Fact]
    public async Task Saving_with_no_connection_says_so_rather_than_failing_silently()
    {
        using var context = new PanelContext();
        var panel = await context.OpenAsync(context.InventoryId);
        context.Server.IsUnreachable = true;

        await panel.SaveCommand.ExecuteAsync(null);

        Assert.True(panel.HasMessage);
    }

    private sealed class PanelContext : IDisposable
    {
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-31T10:00:00Z"));

        public PanelContext()
        {
            Server = new FakeInventoryServer(_clock);
            InventoryId = Server.AddInventory("Kitchen").Id;
        }

        public FakeInventoryServer Server { get; }

        public Guid InventoryId { get; }

        public FixedNetworkStatus Network { get; } = FixedNetworkStatus.Online;

        public async Task<RestockListSettingsPanel> OpenAsync(Guid? inventoryServerId)
        {
            var translations = new Translations(new InMemoryLanguageStore());
            var panel = new RestockListSettingsPanel(
                new InventoryClient(Server.ToHttpClient()), translations,
                new ConnectionRequirement(Network, translations));

            await panel.ShowFor(inventoryServerId, CancellationToken.None);
            return panel;
        }

        public void Dispose() => Server.Dispose();
    }
}
