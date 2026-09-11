using Orbit.Contracts.Calendar;
using Orbit.Contracts.Places;
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
    /// <summary>
    /// What the reader types into the password boxes. Named for the same reason SignInScreenTests names
    /// its own: a literal beside a field called Password is what a secret scanner looks for, and it
    /// cannot tell a test from a credential somebody pasted. It read two of these as real ones once.
    /// </summary>
    private const string Current = "sourdough-and-thunder";
    private const string Chosen = "rye-and-lightning";
    private const string Mistyped = "rye-and-lightening";

    /// <summary>
    /// The same, for deleting the account: the one it would accept, and the one it must not.
    /// </summary>
    private const string Real = "the real one";
    private const string Guessed = "a guess";

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
    /// Debugger is not named at all until it has been unlocked - see PermissionListing, which
    /// Orbit.Web's own table reads too. A row saying "Locked" beside the others advertises that there
    /// is a code somewhere for it, to every account, on a screen everybody visits.
    /// </summary>
    [Fact]
    public async Task The_Debugger_permission_is_not_listed_until_it_is_unlocked()
    {
        using var context = new ScreenContext
        {
            Holding = [ApplicationPermission.Contacts, ApplicationPermission.Chat]
        };
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.DoesNotContain(screen.Permissions, permission => permission.Name == "Debugger");
        // Everything else is still named, held or not: "Locked" is the answer to "can I use this".
        Assert.Contains(screen.Permissions, permission => permission.Name == "Location");
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
        screen.CurrentPassword = Current;
        screen.NewPassword = Chosen;
        screen.RepeatedNewPassword = Mistyped;

        await screen.ChangePasswordCommand.ExecuteAsync(null);

        Assert.True(screen.MessageIsFailure);
        Assert.True(screen.HasMessage);
        // Nothing was sent, so nothing was cleared - what was typed is still there to be corrected.
        Assert.Equal(Chosen, screen.NewPassword);
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

    /// <summary>
    /// Places are the one part written out opened, so they wait to be asked for: somebody pressing Export
    /// the way they always have should not come away with a readable file of where they keep the spare
    /// key. The browser starts its box unticked for the same reason.
    /// </summary>
    [Fact]
    public async Task Places_are_left_out_of_an_export_until_they_are_asked_for()
    {
        using var context = new ScreenContext();
        context.Transfer.Archive = context.Transfer.Archive with { Notes = [ANote()], Places = [ASealedPlace()] };

        var screen = context.Open();
        (string FileName, string Json)? offered = null;
        screen.ExportReady += (_, export) => offered = export;

        Assert.False(screen.Export.IncludesPlaces);
        await screen.ExportCommand.ExecuteAsync(null);

        var written = Read(offered!.Value.Json);
        Assert.Single(written.Notes);
        Assert.Empty(written.AllPlaces);
    }

    /// <summary>
    /// Said beside the switch while it is on, and only then - a warning under every export would be read
    /// as boilerplate by the time it mattered.
    /// </summary>
    [Fact]
    public void The_file_is_said_to_be_unencrypted_while_places_are_chosen()
    {
        using var context = new ScreenContext();
        var screen = context.Open();

        Assert.False(screen.HasPlacesWarning);

        screen.Export.IncludesPlaces = true;

        Assert.True(screen.HasPlacesWarning);
        Assert.Contains("not encrypted", screen.PlacesWarning, StringComparison.Ordinal);

        screen.Export.IncludesPlaces = false;

        Assert.False(screen.HasPlacesWarning);
    }

    /// <summary>
    /// Somebody asking for their places is asking to read them somewhere else, and the server cannot
    /// open a sealed one - so the phone opens it with the key it opens the map's pins with. The sealed
    /// half stays beside the words, so the file still imports sealed.
    /// </summary>
    [Fact]
    public async Task Chosen_places_are_written_opened()
    {
        using var context = new ScreenContext();
        var spareKey = new SealedPlace(
            "The spare key", "Under the third pot", new EventLocationDto("Piękna 1, Warszawa", 52.2297, 21.0122));
        context.Transfer.Archive = context.Transfer.Archive with
        {
            Places = [APlace("The park"), ASealedPlace() with { EncryptedContent = context.Seal(spareKey) }]
        };

        var screen = context.Open();
        screen.Export.IncludesPlaces = true;
        (string FileName, string Json)? offered = null;
        screen.ExportReady += (_, export) => offered = export;

        await screen.ExportCommand.ExecuteAsync(null);

        var written = Read(offered!.Value.Json).AllPlaces;
        Assert.Equal(new[] { "The park", "The spare key" }, written.Select(place => place.Name));
        var opened = written[1];
        Assert.Equal("Under the third pot", opened.Description);
        Assert.Equal("Piękna 1, Warszawa", opened.Where.Address);
        Assert.Equal(52.2297, opened.Where.Latitude);
        Assert.NotNull(opened.EncryptedContent);
        Assert.Equal("Exported 0 notes, 0 task lists, 0 events, 0 inventories and 2 places.", screen.TransferMessage);
        Assert.False(screen.HasTransferWarning);
    }

    /// <summary>
    /// A place sealed under a key pair since replaced cannot be opened here. It still goes into the file,
    /// with its empty words, rather than failing the whole export - and the screen says how many did.
    /// </summary>
    [Fact]
    public async Task A_place_this_phone_cannot_open_goes_out_empty_and_is_counted()
    {
        using var context = new ScreenContext();
        var sealedElsewhere = SealedUnderAnotherKey(
            new SealedPlace("Somebody's secret", string.Empty, new EventLocationDto("Hidden", 1, 2)));
        context.Transfer.Archive = context.Transfer.Archive with
        {
            Places = [ASealedPlace() with { EncryptedContent = sealedElsewhere }]
        };

        var screen = context.Open();
        screen.Export.IncludesPlaces = true;
        (string FileName, string Json)? offered = null;
        screen.ExportReady += (_, export) => offered = export;

        await screen.ExportCommand.ExecuteAsync(null);

        var written = Assert.Single(Read(offered!.Value.Json).AllPlaces);
        Assert.Equal(string.Empty, written.Name);
        Assert.Equal(0, written.Where.Latitude);
        Assert.True(screen.HasTransferWarning);
        Assert.StartsWith("1 private places", screen.TransferWarning, StringComparison.Ordinal);
    }

    /// <summary>
    /// An export that leaves places out never reaches for the key - a phone without one exports the rest
    /// exactly as it did before places could be chosen, and has nothing to warn about.
    /// </summary>
    [Fact]
    public async Task An_export_without_places_says_nothing_about_them_being_unopened()
    {
        using var context = new ScreenContext();
        context.Transfer.Archive = context.Transfer.Archive with { Places = [ASealedPlace()] };
        var screen = context.Open();

        await screen.ExportCommand.ExecuteAsync(null);

        Assert.False(screen.HasTransferWarning);
    }

    /// <summary>
    /// Five counts, as the browser says them: an import that brought places back and did not say so
    /// would leave the reader wondering whether the file had carried them at all.
    /// </summary>
    [Fact]
    public async Task Importing_says_how_many_places_came_back_too()
    {
        using var context = new ScreenContext();
        var screen = context.Open();
        var file = new OrbitArchive(
            OrbitArchive.CurrentVersion, DateTimeOffset.UtcNow, [ANote()], [], [], [], [APlace("The park")]);

        await screen.ImportAsync(JsonSerializer.Serialize(file));

        Assert.Equal("Imported 1 notes, 0 task lists, 0 events, 0 inventories and 1 places.", screen.TransferMessage);
    }

    private static OrbitArchive Read(string json)
        => JsonSerializer.Deserialize<OrbitArchive>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    /// <summary>An open place as the server writes one into an export: its words readable, nothing sealed.</summary>
    private static ArchivedPlace APlace(string name)
        => new(name, string.Empty, new ArchivedEventLocation("Park Skaryszewski", 52.24, 21.05), "", "Normal", [],
            IsPrivate: false, EncryptedContent: null);

    /// <summary>Sealed by another account's key - what a phone holding a replaced key pair cannot open.</summary>
    private static ArchivedEncryptedContent SealedUnderAnotherKey(SealedPlace place)
    {
        using var key = PrivateContent.WithAKey().UnlockAsync().GetAwaiter().GetResult();
        var sealedContent = key.Seal(place, SealedContentSerializerContext.Default.SealedPlace);
        return new ArchivedEncryptedContent(sealedContent.Ciphertext, sealedContent.Nonce);
    }

    /// <summary>
    /// A file the browser wrote carries its private places opened. Importing it here still sends them up
    /// closed: the server was never meant to read one, and it restores them from the sealed half.
    /// </summary>
    [Fact]
    public async Task An_imported_files_private_places_reach_the_server_closed()
    {
        using var context = new ScreenContext();
        var screen = context.Open();
        var file = new OrbitArchive(
            OrbitArchive.CurrentVersion, DateTimeOffset.UtcNow, [], [], [], [],
            [ASealedPlace() with
            {
                Name = "The spare key",
                Where = new ArchivedEventLocation("Piękna 1, Warszawa", 52.2297, 21.0122)
            }]);

        await screen.ImportAsync(JsonSerializer.Serialize(file));

        var sent = Assert.Single(context.Transfer.Imported!.AllPlaces);
        Assert.Equal(string.Empty, sent.Name);
        Assert.Equal(0, sent.Where.Latitude);
        Assert.Equal("c2VhbGVk", sent.EncryptedContent!.Ciphertext);
    }

    /// <summary>A private place as the server writes one into an export: empty words beside its sealed half.</summary>
    private static ArchivedPlace ASealedPlace()
        => new(string.Empty, string.Empty, new ArchivedEventLocation(string.Empty, 0, 0), "", "Normal", [],
            IsPrivate: true, new ArchivedEncryptedContent("c2VhbGVk", "bm9uY2U="));

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
        context.Users.DeletionPassword = Real;
        context.Keep(new LocalNote { Title = "Bank details" });
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);
        screen.DeleteAccountPassword = Real;

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
        context.Users.DeletionPassword = Real;
        context.Keep(new LocalNote { Title = "Bank details" });
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);
        screen.DeleteAccountPassword = Guessed;

        await screen.DeleteAccountCommand.ExecuteAsync(null);

        Assert.False(context.Users.AccountDeleted);
        // Said inside the danger card, where the reader pressed the button - see DeletionMessage.
        Assert.True(screen.HasDeletionMessage);
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

    /// <summary>
    /// Where this deployment offers Google to the phone, an account without a password confirms its
    /// deletion with Google - asked again, and checked by the server - rather than by typing its address,
    /// which anybody holding the unlocked phone could do as easily as its owner.
    /// </summary>
    [Fact]
    public async Task Where_google_is_offered_a_passwordless_account_confirms_with_it()
    {
        using var context = new ScreenContext();
        context.Users.Account = context.Users.Account with { HasPassword = false, IsGoogleLinked = true };
        context.Users.GoogleAndroidClientId = "android-client";
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.ConfirmsWithGoogle);
        Assert.False(screen.RequiresTypedAccountToDelete);
        Assert.True(screen.IsReadyToDelete());
    }

    [Fact]
    public async Task A_fresh_google_sign_in_deletes_the_account_and_empties_the_device()
    {
        using var context = new ScreenContext();
        context.Users.Account = context.Users.Account with { HasPassword = false, IsGoogleLinked = true };
        context.Users.GoogleAndroidClientId = "android-client";
        context.Keep(new LocalNote { Title = "Bank details" });
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.DeleteWithGoogleCommand.ExecuteAsync(null);

        Assert.True(context.Users.AccountDeleted);
        Assert.Equal("the-id-token", context.Users.LastDeletionGoogleIdToken);
        Assert.Null(await context.Session.GetAsync());
        Assert.Equal("ShowSignIn", context.Navigator.LastDestination);
    }

    /// <summary>A sign-in the server does not take leaves the account and the device, and says why.</summary>
    [Fact]
    public async Task A_google_sign_in_the_server_refuses_leaves_everything_and_says_so()
    {
        using var context = new ScreenContext();
        context.Users.Account = context.Users.Account with { HasPassword = false, IsGoogleLinked = true };
        context.Users.GoogleAndroidClientId = "android-client";
        context.Users.GoogleIssuesToken = "an-old-token";
        context.Keep(new LocalNote { Title = "Bank details" });
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.DeleteWithGoogleCommand.ExecuteAsync(null);

        Assert.False(context.Users.AccountDeleted);
        Assert.Equal("Google didn't confirm this account. Try again.", screen.DeletionMessage);
        Assert.NotNull(await context.Session.GetAsync());
        using var store = context.Store.CreateDbContext();
        Assert.NotEmpty(store.Notes);
    }

    /// <summary>Backing out of Google's screen is the reader changing their mind: nothing is sent and nothing is said.</summary>
    [Fact]
    public async Task Backing_out_of_google_deletes_nothing()
    {
        using var context = new ScreenContext();
        context.Users.Account = context.Users.Account with { HasPassword = false, IsGoogleLinked = true };
        context.Users.GoogleAndroidClientId = "android-client";
        context.SignInBrowser = new FakeSignInBrowser { Result = null };
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.DeleteWithGoogleCommand.ExecuteAsync(null);

        Assert.Equal(0, context.Users.DeletionRequests);
        Assert.False(screen.HasDeletionMessage);
    }

    /// <summary>Where Google is not offered, the typed address stays the way - the server has nothing else to check yet.</summary>
    [Fact]
    public async Task Where_google_is_not_offered_the_address_is_typed_instead()
    {
        using var context = new ScreenContext();
        context.Users.Account = context.Users.Account with { HasPassword = false, IsGoogleLinked = true };
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(screen.ConfirmsWithGoogle);
        Assert.True(screen.RequiresTypedAccountToDelete);
    }

    /// <summary>
    /// An account that signs in with Google can hold a password without thinking of itself as having
    /// one, so it is told which one is meant - and every account asked for one is offered the way to a
    /// new one, since a reset is how an account whose password is gone gets deleted at all.
    /// </summary>
    [Fact]
    public async Task A_Google_account_with_a_password_is_told_which_one_and_offered_a_reset()
    {
        using var context = new ScreenContext();
        context.Users.Account = context.Users.Account with { HasPassword = true, IsGoogleLinked = true };
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.RequiresPasswordToDelete);
        Assert.True(screen.ExplainsWhichPassword);
        Assert.False(screen.RequiresTypedAccountToDelete);

        screen.GoToPasswordResetCommand.Execute(null);

        Assert.Equal("ShowPasswordReset", context.Navigator.LastDestination);
    }

    /// <summary>An account that has only ever had a password has nothing to be confused about.</summary>
    [Fact]
    public async Task An_account_without_Google_is_not_told_about_Google()
    {
        using var context = new ScreenContext();
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.RequiresPasswordToDelete);
        Assert.False(screen.ExplainsWhichPassword);
    }

    /// <summary>
    /// An account with no password has nothing to prove itself with, and the server asks it for nothing -
    /// so typing its address or login is what stands between one stray press and everything it holds.
    /// Nothing is sent until what was typed matches the account this screen loaded.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("somebody@else.example")]
    public async Task A_passwordless_account_is_not_deleted_until_it_types_its_address(string typed)
    {
        using var context = new ScreenContext();
        context.Users.Account = context.Users.Account with { HasPassword = false, IsGoogleLinked = true };
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);
        screen.DeleteAccountConfirmation = typed;

        Assert.True(screen.RequiresTypedAccountToDelete);
        Assert.False(screen.IsReadyToDelete());
        await screen.DeleteAccountCommand.ExecuteAsync(null);

        Assert.Equal(0, context.Users.DeletionRequests);
        Assert.False(context.Users.AccountDeleted);
        Assert.True(screen.HasDeletionMessage);
        Assert.Null(context.Navigator.LastDestination);
    }

    /// <summary>
    /// Either of the two things it signs in with, in any case and with stray spaces ignored - both are
    /// things the reader knows without looking them up.
    /// </summary>
    [Theory]
    [InlineData(" ME@Orbit.Example ")]
    [InlineData("Me")]
    public async Task A_passwordless_account_that_types_its_address_or_login_is_deleted(string typed)
    {
        using var context = new ScreenContext();
        context.Users.Account = context.Users.Account with { HasPassword = false, IsGoogleLinked = true };
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);
        screen.DeleteAccountConfirmation = typed;

        Assert.True(screen.IsReadyToDelete());
        await screen.DeleteAccountCommand.ExecuteAsync(null);

        Assert.True(context.Users.AccountDeleted);
        Assert.Equal("ShowSignIn", context.Navigator.LastDestination);
    }

    /// <summary>
    /// Checked against the account the server described, not the login box on this screen - the reader
    /// can type anything into that, and a check it could satisfy would check nothing.
    /// </summary>
    [Fact]
    public async Task The_login_box_on_this_screen_is_not_what_the_typed_login_is_checked_against()
    {
        using var context = new ScreenContext();
        context.Users.Account = context.Users.Account with { HasPassword = false };
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);
        screen.UserName = "anything";
        screen.DeleteAccountConfirmation = "anything";

        Assert.False(screen.IsReadyToDelete());
    }

    /// <summary>
    /// Without the account there is no telling what to ask for, and guessing "nothing" is the one wrong
    /// guess that deletes something - so nothing is sent before it has loaded, password typed or not.
    /// </summary>
    [Fact]
    public async Task Nothing_is_deleted_before_the_account_has_loaded()
    {
        using var context = new ScreenContext();
        context.Users.DeletionPassword = Real;
        var screen = context.Open();
        screen.DeleteAccountPassword = Real;

        Assert.False(screen.IsReadyToDelete());
        await screen.DeleteAccountCommand.ExecuteAsync(null);

        Assert.Equal(0, context.Users.DeletionRequests);
        Assert.False(context.Users.AccountDeleted);
        Assert.True(screen.HasDeletionMessage);
    }

    /// <summary>An account with a password is not sent to the server without one - it could only be refused.</summary>
    [Fact]
    public async Task An_account_with_a_password_is_asked_for_it_before_anything_is_sent()
    {
        using var context = new ScreenContext();
        context.Users.DeletionPassword = Real;
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.DeleteAccountCommand.ExecuteAsync(null);

        Assert.Equal(0, context.Users.DeletionRequests);
        Assert.Equal("Enter your password to confirm.", screen.DeletionMessage);
    }

    /// <summary>
    /// A password set since the screen read the account - on another device, or at the chat gate - makes
    /// the server refuse a deletion that asked for none. The screen reads the account again, so the
    /// password field is there to be filled rather than the same refusal waiting on the next press.
    /// </summary>
    [Fact]
    public async Task A_password_set_since_the_screen_loaded_is_asked_for_next()
    {
        using var context = new ScreenContext();
        context.Users.Account = context.Users.Account with { HasPassword = false };
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);
        context.Users.Account = context.Users.Account with { HasPassword = true };
        context.Users.DeletionPassword = Real;
        screen.DeleteAccountConfirmation = "me";

        await screen.DeleteAccountCommand.ExecuteAsync(null);

        Assert.False(context.Users.AccountDeleted);
        Assert.Equal("This account has a password now - enter it.", screen.DeletionMessage);
        Assert.True(screen.RequiresPasswordToDelete);
        Assert.False(screen.RequiresTypedAccountToDelete);
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

        private readonly SessionStore _sessionStore;

        /// <summary>This device's own key for the signed-in account - what opens its sealed places.</summary>
        private readonly InMemoryChatKeyStorage _keys = new();

        public ScreenContext()
        {
            var userId = Guid.NewGuid();
            _sessionStore = new(new InMemorySessionStorage(
                new UserSession("access", "refresh", userId, "me@orbit.example", "Me")));

            using var identity = ChatIdentity.Create();
            _keys.WritePrivateKeyJwkAsync(userId, identity.ExportPrivateKeyJwk()).GetAwaiter().GetResult();
        }

        public SessionStore Session => _sessionStore;

        /// <summary>A place sealed under this device's key, as the server would hand it back in an export.</summary>
        public ArchivedEncryptedContent Seal(SealedPlace place)
        {
            using var key = Sealer().UnlockAsync().GetAwaiter().GetResult();
            var sealedContent = key.Seal(place, SealedContentSerializerContext.Default.SealedPlace);
            return new ArchivedEncryptedContent(sealedContent.Ciphertext, sealedContent.Nonce);
        }

        private PrivateContentSealer Sealer() => new(_keys, _sessionStore);

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
                new TransferClient(Transfer.ToHttpClient(), Sealer()),
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
                    new GoogleSignIn(SignInBrowser, _users.ToHttpClient()),
                    new Translations(new InMemoryLanguageStore())),
                GoogleExtras);

        /// <summary>The system browser Google is opened in - a reader who signs in, unless a test says they back out.</summary>
        public FakeSignInBrowser SignInBrowser { get; set; } = new();

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
