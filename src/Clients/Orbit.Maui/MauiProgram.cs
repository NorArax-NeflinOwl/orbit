using Microsoft.Extensions.Logging;
using Orbit.Core.Mobile;
using Orbit.Maui.Configuration;
using Orbit.Maui.Features.Authentication;
using Orbit.Maui.Features.Notes;
using Orbit.Maui.Features.Startup;
using Orbit.Maui.Platform;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Update;

namespace Orbit.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		RegisterPlatformServices(builder.Services);
		RegisterHttpClients(builder.Services, OrbitApiSettings.Development);
		RegisterScreens(builder.Services);

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	private static void RegisterPlatformServices(IServiceCollection services)
	{
		services.AddSingleton(SecureStorage.Default);
		services.AddSingleton(Preferences.Default);
		services.AddSingleton<ISessionStorage, SecureSessionStorage>();
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

		services.AddHttpClient<TokenRefreshService>(client => client.BaseAddress = apiSettings.BaseAddress);
		services.AddHttpClient<AuthenticationClient>(client => client.BaseAddress = apiSettings.BaseAddress);
		services.AddHttpClient<MobileVersionGate>(client => client.BaseAddress = apiSettings.BaseAddress);
	}

	private static void RegisterScreens(IServiceCollection services)
	{
		services.AddSingleton<AppNavigator>();

		services.AddTransient<StartupPage>();
		services.AddTransient<StartupViewModel>();
		services.AddTransient<SignInPage>();
		services.AddTransient<SignInViewModel>();
		services.AddTransient<NotesPage>();
		services.AddTransient<NotesViewModel>();
	}
}
