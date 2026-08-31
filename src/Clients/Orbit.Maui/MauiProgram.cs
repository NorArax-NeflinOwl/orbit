using Microsoft.Extensions.Logging;
using Orbit.Core.Mobile;
using Orbit.Maui.Configuration;
using Orbit.Maui.Diagnostics;
using Orbit.Maui.Features.Account;
using Orbit.Mobile.Screens.Account;
using Orbit.Mobile.Screens.Authentication;
using Orbit.Mobile.Screens.Calendar;
using Orbit.Mobile.Screens.Chat;
using Orbit.Mobile.Screens.Inventory;
using Orbit.Mobile.Screens.Location;
using Orbit.Mobile.Location;
using Orbit.Mobile.Screens.Dashboard;
using Orbit.Mobile.Screens.Diagnostics;
using Orbit.Mobile.Screens.Navigation;
using Orbit.Mobile.Screens.Notes;
using Orbit.Mobile.Screens.Sharing;
using Orbit.Mobile.Screens.Suggestions;
using Orbit.Mobile.Screens.Notifications;
using Orbit.Mobile.Diagnostics;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Permissions;
using Orbit.Mobile.Presence;
using Orbit.Mobile.Security;
using Orbit.Mobile.Screens.Startup;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Screens;
using Orbit.Maui.Features.Authentication;
using Orbit.Maui.Features.Calendar;
using Orbit.Maui.Features.Copies;
using Orbit.Mobile.Screens.Copies;
using Orbit.Maui.Features.Chat;
using Orbit.Maui.Features.Inventory;
using Orbit.Maui.Features.Location;
using Orbit.Maui.Features.Dashboard;
using Orbit.Maui.Features.Diagnostics;
using Orbit.Maui.Features.Notes;
using Orbit.Maui.Features.Notifications;
using Orbit.Maui.Features.Tasks;
using Orbit.Maui.Features.Startup;
using Orbit.Maui.Platform;
using Orbit.Mobile.Api;
using Microsoft.EntityFrameworkCore;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Update;

namespace Orbit.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiMaps()
			.ConfigureFonts(fonts =>
			{
				// The two faces Orbit.Web loads (see its index.html): IBM Plex Sans for anything read as
				// text, Space Grotesk for headings. Same faces in both clients, which is the difference
				// between two products that share a palette and one product.
				fonts.AddFont("IBMPlexSans-Regular.ttf", "OrbitBody");
				fonts.AddFont("IBMPlexSans-SemiBold.ttf", "OrbitBodySemibold");
				fonts.AddFont("SpaceGrotesk-SemiBold.ttf", "OrbitDisplay");
			});

		RegisterPlatformServices(builder.Services);
		RegisterLocalStore(builder.Services);
		RegisterHttpClients(builder.Services, OrbitApiSettings.Current);
		RegisterScreens(builder.Services);

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Everything the app already writes through ILogger also lands in a capped file on the phone, so
		// a report can be sent from the diagnostics screen without anybody having to remember to log
		// anything specially. Built here rather than resolved, because logging is configured before the
		// container is.
		var diagnosticLog = new DiagnosticLogFile(FileSystem.AppDataDirectory, TimeProvider.System);
		var diagnosticVerbosity = new DiagnosticLogVerbosity();
		builder.Services.AddSingleton(diagnosticLog);
		builder.Services.AddSingleton(diagnosticVerbosity);
		builder.Logging.AddProvider(new DiagnosticLogProvider(diagnosticLog, diagnosticVerbosity));

		// And the failures nothing handles, which the provider above never sees: the process ends
		// before anything writes them - see CrashLogging.
		CrashLogging.Watch(new CrashLog(diagnosticLog));

		var app = builder.Build();
		LocalDatabase.Migrate(app.Services);
		return app;
	}

	/// <summary>
	/// The offline half of the app (info/orbit-maui-plan.md §5). Screens read notes from here and never
	/// from the API; the synchroniser is what keeps the two in step.
	/// </summary>
	private static void RegisterLocalStore(IServiceCollection services)
	{
		// A factory rather than a context: there is no request to scope one to, and a context living as
		// long as a screen holds every entity it ever loaded and a SQLite connection behind it.
		services.AddDbContextFactory<OrbitLocalDbContext>(options => options.UseSqlite(LocalDatabase.ConnectionString));
		services.AddSingleton(TimeProvider.System);
		services.AddSingleton<INetworkStatus, DeviceNetworkStatus>();
		// Transient: each screen binds its own, and one that outlived its screen would keep it alive
		// through the connectivity subscription.
		services.AddTransient<ConnectionRequirement>();
		// Shared, because the synchronisers are transient and the thing being guarded is the database.
		services.AddSingleton<SyncGate>();
		// One instance: it reads the key this device already holds and nothing else - no network, no state
		// of its own - so the repositories that seal with it can stay singletons too.
		services.AddSingleton<PrivateContentSealer>();
		services.AddSingleton<LocalNoteRepository>();
		services.AddSingleton<LocalTaskListRepository>();
		services.AddSingleton<LocalCalendarEventRepository>();
		services.AddSingleton<LocalWarehouseRepository>();

		// Transient, not singleton: both take a typed HttpClient, and holding one for the life of the app
		// pins the handler underneath it forever - which is the thing IHttpClientFactory exists to rotate.
		services.AddTransient<OwnEncryptionKeyProvider>();
		services.AddTransient<NoteSynchronizer>();
		services.AddTransient<TaskListSynchronizer>();
		services.AddTransient<CalendarEventSynchronizer>();
		services.AddTransient<NotificationSynchronizer>();
		services.AddTransient<LocalNotificationRepository>();
		services.AddTransient<PendingCalendarLinkResolver>();
		services.AddTransient<WarehouseSynchronizer>();
		services.AddSingleton<ChatRepository>();
		services.AddTransient<LocalStoreReset>();
		// The steps every way in shares once the server has accepted somebody - see SignInCompletion.
		services.AddTransient<SignInCompletion>();
		services.AddTransient<EncryptedChatMessageReader>();
		services.AddTransient<EncryptedChatMessageSender>();
		services.AddTransient<ChatSynchronizer>();
		services.AddTransient<EverythingSynchronizer>();
		services.AddTransient<ChatDirectoryReader>();
		services.AddTransient<EncryptedChatMessageEditor>();
		services.AddTransient<MessageForwarder>();
		services.AddTransient<GroupHistorySharing>();
		services.AddTransient<SharedItemAcceptance>();
		services.AddTransient<SharedItemSharing>();
		services.AddTransient<SharePanel>();
		// One per editor screen rather than one for the app: it holds what is being typed into the field
		// that is open, and two screens sharing it would offer each other's names.
		services.AddTransient<NameSuggestions>();
		services.AddTransient<StockCheckPanel>();
		services.AddTransient<GoogleAccountLink>();
		services.AddTransient<SharedLocations>();
		services.AddTransient<NotificationOpener>();
		// One holder for the whole app: the tap is recorded by platform code and taken by whatever
		// screen is ready to follow it, which only works if both see the same instance.
		services.AddSingleton<PendingNotificationTap>();
		// One instance, because it is where the app is rather than what a screen knows: the navigator
		// writes to it on every move and the phone's back gesture reads it - see UpNavigation.
		services.AddSingleton<UpNavigation>();
		// Both are singletons because they describe the app rather than a screen: every navigation
		// bar reads the same presence, and every section reports into the same sync state.
		services.AddSingleton<Orbit.Mobile.Presence.Presence>();
		services.AddSingleton<SyncState>();
		// One instance: every screen reads the same chosen language.
		services.AddSingleton<Translations>();
		// One instance: the navigation bar, the screens it leads to and the account screen all have to
		// agree about what this account may use, and only one page is ever on screen.
		services.AddSingleton<UserPermissions>();
		// One answer for the app, like the permissions above: the map and the calendar both ask whether
		// this account may hand something off to Google, and neither should cost its own round trip.
		services.AddSingleton<Orbit.Mobile.Google.GoogleIntegrationAccess>();
		// One heartbeat for the app, started and stopped with the window - see PresenceReporter.
		services.AddSingleton<PresenceReporter>();
		// One gate for the whole app: unlocking private things on one screen unlocks them everywhere,
		// and putting the phone down locks them everywhere.
		services.AddSingleton<PrivateItemGate>();
		services.AddTransient<PushRegistration>();
		services.AddSingleton<IDeviceLocation, PhoneLocation>();
		services.AddSingleton<IPlacePicker, PhonePlacePicker>();
		services.AddSingleton<IDevicePushNotifications, PhonePushNotifications>();
		services.AddSingleton<IPresenceStore, PreferencesPresenceStore>();
		services.AddSingleton<IDashboardPinStore, PreferencesDashboardPinStore>();
		services.AddSingleton<IDashboardCardPreferenceStore, PreferencesDashboardCardPreferenceStore>();
		services.AddSingleton<IThemeStore, PreferencesThemeStore>();
		services.AddSingleton<IAccentColorStore, PreferencesAccentColorStore>();
		services.AddSingleton<ILanguageStore, PreferencesLanguageStore>();
		services.AddSingleton<IDeviceDescription, PhoneDescription>();
		// The browser half of signing in with Google - see IWebSignInBrowser for why it lives in the head.
		services.AddSingleton<IWebSignInBrowser, WebSignInBrowser>();
		services.AddSingleton<IDeviceAuthentication, PhoneAuthentication>();
		// The same rule for OpenStreetMap's Nominatim - a third-party host, so no Orbit token goes near
		// it. Orbit.Web registers its own copy the same way. Nominatim asks callers to identify
		// themselves, and refuses ones that do not.
		services.AddHttpClient<PlaceSearch>(client =>
		{
			client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
			client.DefaultRequestHeaders.UserAgent.ParseAdd("Orbit/1.0 (https://github.com/NorArax-NeflinOwl/orbit)");
		});
	}

	private static void RegisterPlatformServices(IServiceCollection services)
	{
		services.AddSingleton(SecureStorage.Default);
		services.AddSingleton(Preferences.Default);
		services.AddSingleton(Connectivity.Current);
		services.AddSingleton<ISessionStorage, SecureSessionStorage>();
		services.AddSingleton<IChatKeyStorage, SecureChatKeyStorage>();
		services.AddSingleton<IVersionVerdictCache, PreferencesVersionVerdictCache>();
		services.AddSingleton<ITaskListArrangementStore, PreferencesTaskListArrangementStore>();
		services.AddSingleton<SessionStore>();

		// What the app reports about itself to the version gate. Read from the build rather than
		// declared here, so it cannot drift from the version actually installed.
		services.AddSingleton(new AppVersion(RunningPlatform, AppInfo.Current.VersionString));
	}

	private static MobilePlatform RunningPlatform =>
#if ANDROID
		MobilePlatform.Android;
#else
		MobilePlatform.Ios;
#endif

	/// <summary>
	/// Three clients, and the separation between them is not incidental. Only the API client carries the
	/// access token; the other two must not, because refreshing through the token handler would recurse
	/// into the retry that called it, and the version gate has to work for a build too old to sign in.
	/// </summary>
	private static void RegisterHttpClients(IServiceCollection services, OrbitApiSettings apiSettings)
	{
		services.AddTransient<AuthorizationMessageHandler>();

		services.AddHttpClient<NotesClient>(client => client.BaseAddress = apiSettings.BaseAddress)
			.AddHttpMessageHandler<AuthorizationMessageHandler>();
		services.AddHttpClient<TasksClient>(client => client.BaseAddress = apiSettings.BaseAddress)
			.AddHttpMessageHandler<AuthorizationMessageHandler>();
		services.AddHttpClient<CalendarClient>(client => client.BaseAddress = apiSettings.BaseAddress)
			.AddHttpMessageHandler<AuthorizationMessageHandler>();
		services.AddHttpClient<InventoryClient>(client => client.BaseAddress = apiSettings.BaseAddress)
			.AddHttpMessageHandler<AuthorizationMessageHandler>();
		services.AddHttpClient<SuggestionsClient>(client => client.BaseAddress = apiSettings.BaseAddress)
			.AddHttpMessageHandler<AuthorizationMessageHandler>();

		// Talks to Google rather than to Orbit, so it gets no base address and no authorization handler -
		// both endpoints it uses are absolute, and Orbit's token means nothing to Google.
		services.AddHttpClient<GoogleSignIn>();

		services.AddHttpClient<TokenRefreshService>(client => client.BaseAddress = apiSettings.BaseAddress);
		services.AddHttpClient<AuthenticationClient>(client => client.BaseAddress = apiSettings.BaseAddress);
		// Registering has no token to attach, and the rest are guarded by the server checking the current
		// password rather than by this client - see AccountClient.
		services.AddHttpClient<AccountClient>(client => client.BaseAddress = apiSettings.BaseAddress)
			.AddHttpMessageHandler<AuthorizationMessageHandler>();
		services.AddHttpClient<EncryptionKeyClient>(client => client.BaseAddress = apiSettings.BaseAddress)
			.AddHttpMessageHandler<AuthorizationMessageHandler>();
		services.AddHttpClient<ChatClient>(client => client.BaseAddress = apiSettings.BaseAddress)
			.AddHttpMessageHandler<AuthorizationMessageHandler>();
		services.AddHttpClient<UsersClient>(client => client.BaseAddress = apiSettings.BaseAddress)
			.AddHttpMessageHandler<AuthorizationMessageHandler>();
		services.AddHttpClient<LocationClient>(client => client.BaseAddress = apiSettings.BaseAddress)
			.AddHttpMessageHandler<AuthorizationMessageHandler>();
		services.AddHttpClient<PublicShareClient>(client => client.BaseAddress = apiSettings.BaseAddress)
			.AddHttpMessageHandler<AuthorizationMessageHandler>();
		services.AddHttpClient<TransferClient>(client => client.BaseAddress = apiSettings.BaseAddress)
			.AddHttpMessageHandler<AuthorizationMessageHandler>();
		services.AddHttpClient<NotificationsClient>(client => client.BaseAddress = apiSettings.BaseAddress)
			.AddHttpMessageHandler<AuthorizationMessageHandler>();
		services.AddHttpClient<DiagnosticsClient>(client => client.BaseAddress = apiSettings.BaseAddress)
			.AddHttpMessageHandler<AuthorizationMessageHandler>();
		services.AddHttpClient<MobileVersionGate>(client => client.BaseAddress = apiSettings.BaseAddress);
		// No authorization handler, like the gate above: which build the server is, is the answer to
		// "what am I talking to" - exactly the question somebody has when they cannot sign in.
		services.AddHttpClient<ServerVersionClient>(client => client.BaseAddress = apiSettings.BaseAddress);
	}

	private static void RegisterScreens(IServiceCollection services)
	{
		// One instance, reachable both ways: the shell asks for AppNavigator, the view models ask for the
		// interface, and swapping the page has to happen on the same object either way.
		services.AddSingleton<AppNavigator>();
		services.AddSingleton<IScreenNavigator>(services => services.GetRequiredService<AppNavigator>());
		services.AddSingleton<IUpdateLink, StoreUpdateLink>();

		services.AddTransient<DashboardPage>();
		services.AddTransient<DashboardViewModel>();
		// Shared by the bar and the menu it opens, which have to agree about whether that menu is open.
		services.AddSingleton<NavigationBarViewModel>();
		services.AddTransient<StartupPage>();
		services.AddTransient<StartupViewModel>();
		services.AddTransient<SignInPage>();
		services.AddTransient<SignInViewModel>();
		services.AddTransient<RegisterPage>();
		services.AddTransient<RegisterViewModel>();
		services.AddTransient<AccountPage>();
		services.AddTransient<AccountViewModel>();
		services.AddTransient<ChatKeyGatePage>();
		services.AddTransient<ChatKeyGateViewModel>();
		services.AddTransient<ContactsPage>();
		services.AddTransient<ContactsViewModel>();
		services.AddTransient<ConversationPage>();
		services.AddTransient<ConversationViewModel>();
		services.AddTransient<GroupsPage>();
		services.AddTransient<GroupsViewModel>();
		services.AddTransient<GroupConversationPage>();
		services.AddTransient<GroupConversationViewModel>();
		services.AddTransient<GroupDetailPage>();
		services.AddTransient<GroupDetailViewModel>();
		services.AddTransient<NotesPage>();
		services.AddTransient<NoteDetailPage>();
		services.AddTransient<CalendarEventDetailPage>();
		services.AddTransient<NotesViewModel>();
		// One lock per editor, not one for the app: two editors open at once each hold their own item.
		services.AddTransient<EditLock>();
		services.AddTransient<NoteDetailViewModel>();
		services.AddTransient<CopyReviewPage>();
		services.AddTransient<CopyReviewViewModel>();
		services.AddTransient<CopyHistoryPage>();
		services.AddTransient<CopyHistoryViewModel>();
		// The four repositories, again, as the one thing the two copy screens know them by. Registered
		// against the same instances rather than as separate ones: a copy resolved here has to be the
		// copy the rest of the app is reading.
		services.AddTransient<ICopyReviewStore>(services => services.GetRequiredService<LocalNoteRepository>());
		services.AddTransient<ICopyReviewStore>(services => services.GetRequiredService<LocalTaskListRepository>());
		services.AddTransient<ICopyReviewStore>(services => services.GetRequiredService<LocalCalendarEventRepository>());
		services.AddTransient<ICopyReviewStore>(services => services.GetRequiredService<LocalWarehouseRepository>());
		services.AddTransient<CalendarEventDetailViewModel>();
		services.AddTransient<TasksPage>();
		services.AddTransient<TasksViewModel>();
		services.AddTransient<TaskListDetailPage>();
		services.AddTransient<TaskListDetailViewModel>();
		services.AddTransient<TaskItemSummaryPage>();
		services.AddTransient<TaskItemSummaryViewModel>();
		services.AddTransient<CalendarPage>();
		services.AddTransient<CalendarViewModel>();
		services.AddTransient<MapPage>();
		services.AddTransient<PlacePickerPage>();
		services.AddTransient<MapViewModel>();
		services.AddTransient<InventoryPage>();
		services.AddTransient<InventoryViewModel>();
		services.AddTransient<WarehouseDetailPage>();
		services.AddTransient<WarehouseDetailViewModel>();
		services.AddTransient<NotificationFeedPage>();
		services.AddTransient<Features.Update.UpdatePage>();
		services.AddTransient<Orbit.Mobile.Screens.Update.UpdateViewModel>();
		services.AddTransient<NotificationFeedViewModel>();
		services.AddTransient<NotificationSettingsViewModel>();
		services.AddTransient<DiagnosticsPage>();
		services.AddTransient<DiagnosticsViewModel>();
	}
}
