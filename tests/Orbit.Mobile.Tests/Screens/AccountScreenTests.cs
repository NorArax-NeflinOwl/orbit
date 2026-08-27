using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Account;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The account screen's own shape. It grew into one long scroll - username, email, password,
/// permissions, diagnostics - so it now has the four tabs Orbit.Web's Options page has: changing a
/// password and unlocking a feature are different errands and should not be stacked on each other.
/// </summary>
public sealed class AccountScreenTests
{
    [Fact]
    public void It_opens_on_the_account_tab()
    {
        using var context = new ScreenContext();
        var screen = context.Open();

        Assert.True(screen.IsShowingAccount);
        Assert.False(screen.IsShowingPermissions);
        Assert.Contains(screen.Tabs, tab => tab is { Tab: AccountTab.Account, IsChosen: true });
    }

    [Fact]
    public void Choosing_a_tab_shows_that_one_and_only_that_one()
    {
        using var context = new ScreenContext();
        var screen = context.Open();

        screen.ChooseTabCommand.Execute(screen.Tabs.Single(tab => tab.Tab == AccountTab.Permissions));

        Assert.True(screen.IsShowingPermissions);
        Assert.False(screen.IsShowingAccount);
        Assert.False(screen.IsShowingAppearance);
        Assert.False(screen.IsShowingDebug);
    }

    /// <summary>A theme that took a restart to appear would read as broken, so it is written at once.</summary>
    [Fact]
    public void The_chosen_theme_is_written_down_and_announced()
    {
        using var context = new ScreenContext();
        var screen = context.Open();
        ChosenTheme? announced = null;
        screen.ThemeChanged += (_, theme) => announced = theme;

        screen.Theme = ChosenTheme.Dark;

        Assert.Equal(ChosenTheme.Dark, context.Themes.Read());
        Assert.Equal(ChosenTheme.Dark, announced);
    }

    /// <summary>It is a preference about this device, so it has to survive the screen being rebuilt.</summary>
    [Fact]
    public void The_theme_survives_the_screen_being_opened_again()
    {
        using var context = new ScreenContext();
        context.Open().Theme = ChosenTheme.Light;

        Assert.Equal(ChosenTheme.Light, context.Open().Theme);
    }

    /// <summary>
    /// A file somebody keeps, not a copy the app maintains. Building it is the view model's job; writing
    /// it and handing it somewhere is the page's, which is why this ends in an event rather than a file.
    /// </summary>
    [Fact]
    public async Task Exporting_hands_over_a_named_file()
    {
        using var context = new ScreenContext();
        var screen = context.Open();
        (string FileName, string Json)? offered = null;
        screen.ExportReady += (_, export) => offered = export;

        await screen.ExportCommand.ExecuteAsync(null);

        Assert.NotNull(offered);
        Assert.StartsWith("orbit-export-", offered!.Value.FileName);
        Assert.EndsWith(".json", offered.Value.FileName);
        Assert.Contains("\"version\"", offered.Value.Json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_export_that_could_not_be_built_says_so_and_offers_nothing()
    {
        using var context = new ScreenContext();
        context.Transfer.RefusesToExport = true;
        var screen = context.Open();
        var offered = false;
        screen.ExportReady += (_, _) => offered = true;

        await screen.ExportCommand.ExecuteAsync(null);

        Assert.False(offered);
        Assert.True(screen.HasTransferMessage);
    }

    [Fact]
    public async Task Importing_reads_the_file_and_says_what_came_back()
    {
        using var context = new ScreenContext();
        var screen = context.Open();
        await screen.ExportCommand.ExecuteAsync(null);

        await screen.ImportAsync(
            """{"version":1,"exportedAtUtc":"2026-08-27T10:00:00Z","notes":[],"taskLists":[],"calendarEvents":[],"warehouses":[]}""");

        Assert.NotNull(context.Transfer.Imported);
        Assert.True(screen.HasTransferMessage);
    }

    /// <summary>
    /// A file that is not JSON and JSON of some other shape get the same answer: neither is something
    /// the reader can act on differently, and neither reaches the server.
    /// </summary>
    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"something\":\"else\"}")]
    public async Task A_file_that_is_not_an_export_is_refused_without_asking_the_server(string contents)
    {
        using var context = new ScreenContext();
        var screen = context.Open();

        await screen.ImportAsync(contents);

        Assert.True(screen.HasTransferMessage);
    }

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeUsersServer _users = new();

        public InMemoryThemeStore Themes { get; } = new();

        /// <summary>The account's whole archive, out and back - see TransferClient.</summary>
        public FakeTransferServer Transfer { get; } = new();

        private readonly SessionStore _sessionStore = new(new InMemorySessionStorage(
            new UserSession("access", "refresh", Guid.NewGuid(), "me@orbit.example", "Me")));

        public AccountViewModel Open()
            => new(
                new AccountClient(_users.ToHttpClient(), FixedNetworkStatus.Online, _sessionStore),
                new OwnEncryptionKeyProvider(
                    new InMemoryChatKeyStorage(),
                    new EncryptionKeyClient(new FakeEncryptionKeyServer().ToHttpClient()),
                    _sessionStore,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<OwnEncryptionKeyProvider>.Instance),
                FixedNetworkStatus.Online,
                _sessionStore,
                new Translations(new InMemoryLanguageStore()),
                new UsersClient(_users.ToHttpClient()),
                UnlockedPermissions.For(_localStore),
                Themes,
                new TransferClient(Transfer.ToHttpClient()),
                new RecordingScreenNavigator());

        public void Dispose()
        {
            _users.Dispose();
            Transfer.Dispose();
            _localStore.Dispose();
        }
    }
}
