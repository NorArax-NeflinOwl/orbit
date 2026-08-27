using Microsoft.Extensions.Logging;
using Orbit.Core.Mobile;
using Orbit.Maui.Configuration;
using Orbit.Maui.Features.Account;
using Orbit.Mobile.Screens.Account;
using Orbit.Mobile.Screens.Authentication;
using Orbit.Mobile.Screens.Calendar;
using Orbit.Mobile.Screens.Chat;
using Orbit.Mobile.Screens.Inventory;
using Orbit.Mobile.Screens.Location;
using Orbit.Mobile.Location;
using Orbit.Mobile.Screens.Notes;
using Orbit.Mobile.Screens.Notifications;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Screens.Startup;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Screens;
using Orbit.Maui.Features.Authentication;
using Orbit.Maui.Features.Calendar;
using Orbit.Maui.Features.Chat;
using Orbit.Maui.Features.Inventory;
using Orbit.Maui.Features.Location;
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
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		RegisterPlatformServices(builder.Services);
		RegisterLocalStore(builder.Services);
		RegisterHttpClients(builder.Services, OrbitApiSettings.Development);
		RegisterScreens(builder.Services);

#if DEBUG
		builder.Logging.AddDebug();
#endif

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
		// Shared, because the synchronisers are transient and the thing being guarded is the database.
		services.AddSingleton<SyncGate>();
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
		services.AddTransient<WarehouseSynchronizer>();
		services.AddSingleton<ChatRepository>();
		services.AddTransient<EncryptedChatMessageReader>();
		services.AddTransient<EncryptedChatMessageSender>();
		services.AddTransient<ChatSynchronizer>();
		services.AddTransient<ChatDirectoryReader>();
		services.AddTransient<EncryptedChatMessageEditor>();
		services.AddTransient<MessageForwarder>();
		services.AddTransient<SharedLocations>();
		services.AddTransient<NotificationOpener>();
		// One holder for the whole app: the tap is recorded by platform code and taken by whatever
		// screen is ready to follow it, which only works if both see the same instance.
		services.AddSingleton<PendingNotificationTap>();
		services.AddTransient<PushRegistration>();
		services.AddSingleton<IDeviceLocation, PhoneLocation>();
		services.AddSingleton<IDevicePushNotifications, PhonePushNotifications>();
	}

	private static void RegisterPlatformServices(IServiceCollection services)
	{
		services.AddSingleton(SecureStorage.Default);
		services.AddSingleton(Preferences.Default);
		services.AddSingleton(Connectivity.Current);
		services.AddSingleton<ISessionStorage, SecureSessionStorage>();
		services.AddSingleton<IChatKeyStorage, SecureChatKeyStorage>();
		services.AddSingleton<IVersionVerdictCache, PreferencesVersionVerdictCache>();
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
		services.AddHttpClient<NotificationsClient>(client => client.BaseAddress = apiSettings.BaseAddress)
			.AddHttpMessageHandler<AuthorizationMessageHandler>();
		services.AddHttpClient<MobileVersionGate>(client => client.BaseAddress = apiSettings.BaseAddress);
	}

	private static void RegisterScreens(IServiceCollection services)
	{
		// One instance, reachable both ways: the shell asks for AppNavigator, the view models ask for the
		// interface, and swapping the page has to happen on the same object either way.
		services.AddSingleton<AppNavigator>();
		services.AddSingleton<IScreenNavigator>(services => services.GetRequiredService<AppNavigator>());
		services.AddSingleton<IUpdateLink, StoreUpdateLink>();

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
		services.AddTransient<NotesViewModel>();
		services.AddTransient<TasksPage>();
		services.AddTransient<TasksViewModel>();
		services.AddTransient<TaskListDetailPage>();
		services.AddTransient<TaskListDetailViewModel>();
		services.AddTransient<CalendarPage>();
		services.AddTransient<CalendarViewModel>();
		services.AddTransient<MapPage>();
		services.AddTransient<MapViewModel>();
		services.AddTransient<InventoryPage>();
		services.AddTransient<InventoryViewModel>();
		services.AddTransient<WarehouseDetailPage>();
		services.AddTransient<WarehouseDetailViewModel>();
		services.AddTransient<NotificationFeedPage>();
		services.AddTransient<NotificationFeedViewModel>();
		services.AddTransient<NotificationSettingsPage>();
		services.AddTransient<NotificationSettingsViewModel>();
	}
}
