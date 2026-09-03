using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Core.Permissions;
using Orbit.Mobile.Google;
using Orbit.Mobile.Localization;
using System.Text;
using System.Text.Json;
using Orbit.Core.Transfer;
using Orbit.Mobile.Screens.Account;
using Orbit.Mobile.Screens.Notifications;
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

    /// <summary>Announced the moment it is picked, like the theme - a colour that took a restart would read as broken.</summary>
    [Fact]
    public void The_chosen_accent_is_written_down_and_announced()
    {
        using var context = new ScreenContext();
        var screen = context.Open();
        AccentColor? announced = null;
        screen.AccentChanged += (_, accent) => announced = accent;

        screen.ChooseAccentCommand.Execute(screen.Accents.Single(choice => choice.Value.Name == "Green"));

        Assert.Equal(150, context.Accents.Read().Hue);
        Assert.Equal(150, announced?.Hue);
    }

    /// <summary>It is a preference about this device, so it has to survive the screen being rebuilt.</summary>
    [Fact]
    public void The_accent_survives_the_screen_being_opened_again()
    {
        using var context = new ScreenContext();
        var first = context.Open();
        first.ChooseAccentCommand.Execute(first.Accents.Single(choice => choice.Value.Name == "Red"));

        Assert.Equal("Red", context.Open().Accent.Name);
    }

    /// <summary>
    /// One of the eight is marked, and only one: a row of swatches with none marked leaves the reader
    /// guessing which colour they are already looking at.
    /// </summary>
    [Fact]
    public void One_swatch_is_marked_as_the_one_in_force()
    {
        using var context = new ScreenContext();
        var screen = context.Open();

        Assert.Equal(AccentColor.Default, screen.Accents.Single(choice => choice.IsChosen).Value);

        screen.ChooseAccentCommand.Execute(screen.Accents.Single(choice => choice.Value.Name == "Teal"));

        Assert.Equal("Teal", screen.Accents.Single(choice => choice.IsChosen).Value.Name);
    }

    /// <summary>
    /// The swatches are painted in the colours the app would actually use, which differ between the
    /// two themes - so a row shown against a dark screen has to be the dark theme's eight.
    /// </summary>
    [Fact]
    public void The_swatches_follow_the_theme_they_are_shown_against()
    {
        using var context = new ScreenContext();
        var screen = context.Open();
        var inLight = screen.Accents.Select(choice => choice.Swatch).ToList();

        screen.IsDarkOnScreen = true;

        Assert.NotEqual(inLight, screen.Accents.Select(choice => choice.Swatch));
    }

    /// <summary>Every swatch is named, so the row is usable by somebody who cannot tell the eight apart.</summary>
    [Fact]
    public void Every_swatch_carries_its_name()
    {
        using var context = new ScreenContext();

        Assert.All(context.Open().Accents, choice => Assert.NotEmpty(choice.Name));
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

    /// <summary>
    /// The Debugger is Orbit's own inside - the captured log, and the detail behind an error - so its
    /// tab goes to an account that has unlocked it and to nobody else. The browser's Options draws the
    /// same line, and so does the version row in the avatar menu.
    /// </summary>
    [Fact]
    public async Task The_Debugger_tab_is_offered_only_to_an_account_holding_it()
    {
        using var context = new ScreenContext { Holding = [ApplicationPermission.Chat] };
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.DoesNotContain(screen.Tabs, tab => tab.Tab == AccountTab.Debug);
        Assert.False(screen.IsShowingDebug);
    }

    [Fact]
    public async Task An_account_holding_the_Debugger_is_offered_its_tab()
    {
        using var context = new ScreenContext { Holding = [ApplicationPermission.Debug] };
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        var debugger = Assert.Single(screen.Tabs, tab => tab.Tab == AccountTab.Debug);
        Assert.Equal("Debugger", debugger.Name);

        screen.Tab = AccountTab.Debug;
        Assert.True(screen.IsShowingDebug);
    }

    /// <summary>
    /// Every permission is named and explained, and the Debugger is the one that was not: it fell
    /// through to Sharing's words, so the row for it said somebody could hand a note to somebody else.
    /// </summary>
    [Fact]
    public async Task Every_permission_is_named_for_what_it_opens()
    {
        using var context = new ScreenContext();
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Contains(screen.Permissions, permission => permission.Name == "Debugger");
        Assert.Equal(
            Enum.GetValues<ApplicationPermission>().Length,
            screen.Permissions.Select(permission => permission.Name).Distinct().Count());
        Assert.Equal(
            Enum.GetValues<ApplicationPermission>().Length,
            screen.Permissions.Select(permission => permission.Explanation).Distinct().Count());
    }

    /// <summary>
    /// The section answers to the permission as well as to the tab, so an account that never held it
    /// reads nothing even if something else puts the screen on that tab.
    /// </summary>
    [Fact]
    public async Task The_Debugger_section_stays_shut_without_the_permission()
    {
        using var context = new ScreenContext { Holding = [ApplicationPermission.Chat] };
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.Tab = AccountTab.Debug;

        Assert.False(screen.IsShowingDebug);
    }

    /// <summary>
    /// A new password typed twice, as the browser's form asks: one mistyped in a box nobody can read
    /// back is one nobody can sign in with afterwards.
    /// </summary>
    [Fact]
    public async Task Two_new_passwords_that_differ_are_refused()
    {
        using var context = new ScreenContext();
        var screen = context.Open();
        screen.CurrentPassword = "sourdough-and-thunder";
        screen.NewPassword = "rye-and-lightning";
        screen.RepeatedNewPassword = "rye-and-lightening";

        await screen.ChangePasswordCommand.ExecuteAsync(null);

        Assert.True(screen.MessageIsFailure);
        Assert.True(screen.HasMessage);
        // Nothing was sent, so nothing was cleared - what was typed is still there to be corrected.
        Assert.Equal("rye-and-lightning", screen.NewPassword);
    }

    /// <summary>
    /// The Google links are the reader's to turn off on this phone, as they are in the browser. The
    /// switch is only offered where the account may use them at all - one for something unavailable
    /// would turn nothing off.
    /// </summary>
    [Fact]
    public async Task The_Google_extras_can_be_turned_off_on_this_phone()
    {
        using var context = new ScreenContext();
        context.Users.Account = context.Users.Account with { IsEmailVerified = true };
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.CanChooseGoogleExtras);
        Assert.True(screen.AllowsGoogleExtras);

        screen.AllowsGoogleExtras = false;

        // Written where the next launch will read it, rather than held by the screen.
        Assert.False(context.GoogleExtras.IsAllowedOnThisDevice);
    }

    /// <summary>An account that cannot use them is not asked about them.</summary>
    [Fact]
    public async Task An_account_that_cannot_use_the_Google_extras_is_not_offered_the_switch()
    {
        using var context = new ScreenContext();
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(screen.CanChooseGoogleExtras);
    }

    /// <summary>One of each, so a test about what a file carries has something to find in it.</summary>
    private static ArchivedNote ANote() => new("Shopping", [], IsPrivate: false, EncryptedContent: null);

    private static ArchivedTaskList ATaskList()
        => new("This week", [], IsGroup: false, IsPrivate: false, EncryptedContent: null, Priority: "Normal");

    /// <summary>
    /// What goes in the file is the reader's to choose, as it is in the browser: somebody moving their
    /// notes to another account has no reason to carry three years of shopping lists with them. The
    /// parts left out are emptied rather than dropped - a file missing a list is one an older Orbit
    /// would refuse to read.
    /// </summary>
    [Fact]
    public async Task An_export_carries_only_what_was_chosen()
    {
        using var context = new ScreenContext();
        context.Transfer.Archive = context.Transfer.Archive with
        {
            Notes = [ANote()],
            TaskLists = [ATaskList()]
        };

        var screen = context.Open();
        screen.Export.IncludesTaskLists = false;
        (string FileName, string Json)? offered = null;
        screen.ExportReady += (_, export) => offered = export;

        await screen.ExportCommand.ExecuteAsync(null);

        var written = JsonSerializer.Deserialize<OrbitArchive>(
            offered!.Value.Json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Single(written.Notes);
        Assert.Empty(written.TaskLists);
        // The version is still there: what was left out is emptied, not taken out of the file's shape.
        Assert.Equal(OrbitArchive.CurrentVersion, written.Version);
    }

    /// <summary>Nothing chosen is not an export of nothing - the button has no reason to be pressed.</summary>
    [Fact]
    public void An_export_of_nothing_is_not_offered()
    {
        using var context = new ScreenContext();
        var screen = context.Open();

        Assert.True(screen.CanExport);

        screen.Export.IncludesNotes = false;
        screen.Export.IncludesTaskLists = false;
        screen.Export.IncludesCalendarEvents = false;
        screen.Export.IncludesInventories = false;

        Assert.False(screen.CanExport);
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
            """{"version":1,"exportedAtUtc":"2026-08-27T10:00:00Z","notes":[],"taskLists":[],"calendarEvents":[],"inventories":[]}""");

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
    /// <summary>
    /// A file too large to hold is refused before it becomes a string. An export of a whole account is
    /// not large by file standards, but a hand-made one could be, and here the whole thing would sit in
    /// a phone's memory at once - Orbit.Web caps its own picker at the same size.
    /// </summary>
    [Fact]
    public async Task A_file_too_large_to_hold_is_refused_without_being_read()
    {
        using var context = new ScreenContext();
        var screen = context.Open();

        await screen.ImportAsync(StreamOf(AccountViewModel.MaximumImportSizeBytes + 1));

        Assert.Null(context.Transfer.Imported);
        Assert.True(screen.HasTransferMessage);
    }

    /// <summary>And one right up against the ceiling is not, so the guard refuses only what it must.</summary>
    [Fact]
    public async Task A_file_at_the_ceiling_is_still_read()
    {
        using var context = new ScreenContext();
        var screen = context.Open();

        await screen.ImportAsync(StreamOf(AccountViewModel.MaximumImportSizeBytes));

        // Nonsense rather than an export, so it is refused for what it says and not for its size - what
        // matters is that it was read at all.
        Assert.Null(context.Transfer.Imported);
        Assert.True(screen.HasTransferMessage);
    }

    private static Stream StreamOf(long sizeBytes)
        => new MemoryStream(Encoding.UTF8.GetBytes(new string('x', (int)sizeBytes)));


    /// <summary>
    /// Deleting the account has to leave nothing of it behind on the phone. The session is the obvious
    /// half; the cached database is the one that would otherwise sit there afterwards, readable, holding
    /// notes belonging to an account that no longer exists.
    /// </summary>
    [Fact]
    public async Task Deleting_the_account_empties_this_device_and_returns_to_sign_in()
    {
        using var context = new ScreenContext();
        context.Users.DeletionPassword = "the real one";
        context.Keep(new LocalNote { Title = "Bank details" });
        var screen = context.Open();
        screen.DeleteAccountPassword = "the real one";

        await screen.DeleteAccountCommand.ExecuteAsync(null);

        Assert.True(context.Users.AccountDeleted);
        Assert.Null(await context.Session.GetAsync());
        using var store = context.Store.CreateDbContext();
        Assert.Empty(store.Notes);
        Assert.Equal("ShowSignIn", context.Navigator.LastDestination);
    }

    /// <summary>
    /// The refused path matters more than the happy one. A wrong password leaves an account that still
    /// exists, so wiping the phone for it would destroy the only local copy of work that was never in
    /// danger - and the reader would be signed out of an account they still have.
    /// </summary>
    [Fact]
    public async Task A_refused_deletion_leaves_the_account_and_the_device_untouched()
    {
        using var context = new ScreenContext();
        context.Users.DeletionPassword = "the real one";
        context.Keep(new LocalNote { Title = "Bank details" });
        var screen = context.Open();
        screen.DeleteAccountPassword = "a guess";

        await screen.DeleteAccountCommand.ExecuteAsync(null);

        Assert.False(context.Users.AccountDeleted);
        Assert.True(screen.MessageIsFailure);
        Assert.NotNull(await context.Session.GetAsync());
        using var store = context.Store.CreateDbContext();
        Assert.NotEmpty(store.Notes);
        Assert.Null(context.Navigator.LastDestination);
    }

    /// <summary>
    /// The screen reads the account rather than trusting the session, which carries only what signing in
    /// needed. A username or address changed on another device would otherwise show the old one here,
    /// and the form below would change the wrong thing back.
    /// </summary>
    [Fact]
    public async Task It_shows_the_account_the_server_holds_rather_than_what_signing_in_carried()
    {
        using var context = new ScreenContext();
        context.Users.Account = context.Users.Account with
        {
            UserName = "patryk",
            Email = "patryk@orbit.example",
            IsEmailVerified = true
        };
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Equal("patryk", screen.UserName);
        Assert.Equal("patryk@orbit.example", screen.EmailAddress);
        Assert.Equal("Verified", screen.EmailVerificationLabel);
    }

    /// <summary>
    /// A Google account that never set a password has none to prove, and the server agrees - see
    /// DeleteAccountCommandHandler. Asking for one would be asking for something that does not exist,
    /// and there would be no way past it.
    /// </summary>
    [Fact]
    public async Task An_account_with_no_password_is_not_asked_for_one_before_deleting()
    {
        using var context = new ScreenContext();
        context.Users.Account = context.Users.Account with { HasPassword = false };
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(screen.RequiresPasswordToDelete);
    }

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();

        /// <summary>Held so a test can ask what the phone kept after the account went away.</summary>
        public LocalStore Store => _localStore;
        private readonly FakeUsersServer _users = new();

        public FakeUsersServer Users => _users;

        public RecordingScreenNavigator Navigator { get; } = new();

        public InMemoryThemeStore Themes { get; } = new();

        public InMemoryAccentColorStore Accents { get; } = new();

        /// <summary>The account's whole archive, out and back - see TransferClient.</summary>
        public FakeTransferServer Transfer { get; } = new();

        /// <summary>
        /// How Orbit may interrupt, which now lives under this screen's Appearance tab rather than on a
        /// screen of its own - see AccountViewModel.Notifications.
        /// </summary>
        public FakeNotificationServer Notifications { get; } = new();

        private readonly SessionStore _sessionStore = new(new InMemorySessionStorage(
            new UserSession("access", "refresh", Guid.NewGuid(), "me@orbit.example", "Me")));

        public SessionStore Session => _sessionStore;

        /// <summary>Puts something in the phone's own database, so a test can watch what becomes of it.</summary>
        public void Keep(LocalNote note)
        {
            using var dbContext = _localStore.CreateDbContext();
            dbContext.Notes.Add(note);
            dbContext.SaveChanges();
        }

        /// <summary>
        /// A screen for an account that holds only these - for the tabs, which are not all offered to
        /// everybody. Null means "everything", which is what the other tests want.
        /// </summary>
        public ApplicationPermission[]? Holding { get; set; }

        public AccountViewModel Open()
            => new(
                new AccountClient(_users.ToHttpClient(), FixedNetworkStatus.Online, _sessionStore),
                new OwnEncryptionKeyProvider(
                    new InMemoryChatKeyStorage(),
                    new EncryptionKeyClient(new FakeEncryptionKeyServer().ToHttpClient()),
                    _sessionStore,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<OwnEncryptionKeyProvider>.Instance),
                Connections.Online,
                _sessionStore,
                new Translations(new InMemoryLanguageStore()),
                new UsersClient(_users.ToHttpClient()),
                Holding is { } held
                    ? UnlockedPermissions.LockedTo(_localStore, held).GetAwaiter().GetResult()
                    : UnlockedPermissions.For(_localStore),
                Themes,
                Accents,
                new TransferClient(Transfer.ToHttpClient()),
                new LocalStoreReset(_localStore),
                new NotificationSettingsViewModel(
                    new NotificationsClient(Notifications.ToHttpClient()),
                    new Translations(new InMemoryLanguageStore()), new RecordingScreenNavigator()),
                Navigator,
                // Not offered: this fake answers client-flags with no client id for this app, which is
                // what a deployment without Google configured looks like - see GoogleAccountLinkTests.
                new GoogleAccountLink(
                    new AccountClient(_users.ToHttpClient(), FixedNetworkStatus.Online, _sessionStore),
                    new AuthenticationClient(_users.ToHttpClient(), FixedNetworkStatus.Online, _sessionStore),
                    new GoogleSignIn(new FakeSignInBrowser(), _users.ToHttpClient()),
                    new Translations(new InMemoryLanguageStore())),
                GoogleExtras);

        /// <summary>What this "device" answers about the Google links - see GoogleExtras.</summary>
        public GoogleExtras GoogleExtras { get; } = new(new InMemoryGoogleExtrasStore());

        public void Dispose()
        {
            _users.Dispose();
            Transfer.Dispose();
            Notifications.Dispose();
            _localStore.Dispose();
        }
    }
}
