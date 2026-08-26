using Microsoft.EntityFrameworkCore;
using Orbit.Mobile.Data;

namespace Orbit.Maui.Platform;

/// <summary>
/// Where the phone's own database lives, and the single place that decides how it is opened.
///
/// It sits in app-private storage, so it is covered by the platform's own disk encryption and is not
/// reachable by other apps. The database itself is *not* encrypted, which is a deliberate and recorded
/// trade-off rather than an oversight: private notes are client-encrypted precisely so the server can
/// never read them, and caching them decrypted here moves that exposure onto the device (see
/// info/orbit-maui-plan.md §5.1 and open question 2). Switching to SQLCipher with the key in the
/// platform keystore is a change to <see cref="ConnectionString"/> and the provider registration in
/// MauiProgram - which is why both are kept here rather than spread through the app.
/// </summary>
public static class LocalDatabase
{
	public static string ConnectionString
		=> $"Data Source={Path.Combine(FileSystem.AppDataDirectory, "orbit.db3")}";

	/// <summary>
	/// EnsureCreated rather than migrations, while nothing has shipped and there is no installed schema
	/// to migrate from. The first release that people actually keep data in has to switch to migrations,
	/// because EnsureCreated silently does nothing to a database that already exists - a new column
	/// would simply be missing at runtime.
	/// </summary>
	public static void EnsureCreated(IServiceProvider services)
	{
		using var dbContext = services
			.GetRequiredService<IDbContextFactory<OrbitLocalDbContext>>()
			.CreateDbContext();

		dbContext.Database.EnsureCreated();
	}
}
