using Orbit.Core.Mobile;
using Orbit.Mobile.Authentication;

namespace Orbit.Maui.Platform;

/// <summary>
/// Sends the reader to Google in a Custom Tab and waits for Android to hand the answer back through
/// <see cref="WebAuthenticationCallbackActivity"/>.
///
/// A browser rather than a WebView on purpose, and not only because MAUI's WebAuthenticator uses one:
/// Google refuses the sign-in flow inside an embedded WebView, since an app that owns the WebView can
/// read what is typed into it.
/// </summary>
public sealed class WebSignInBrowser : IWebSignInBrowser
{
	/// <summary>
	/// Built from the package name rather than written out, so it cannot drift from the ApplicationId -
	/// Google will only redirect to the address registered against this app's package and signing
	/// certificate, and the intent filter that catches it is declared on the same name. The client id
	/// plays no part on Android, unlike iOS, so it is ignored here.
	/// </summary>
	private static readonly Uri Callback = new($"{AppInfo.PackageName}:/oauth2redirect");

	public string Platform => nameof(MobilePlatform.Android);

	public Uri CallbackAddressFor(string clientId) => Callback;

	public async Task<IReadOnlyDictionary<string, string>?> SignInAsync(
		Uri startAddress, CancellationToken cancellationToken = default)
	{
		try
		{
			var result = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
			{
				Url = startAddress,
				CallbackUrl = Callback
			});

			return result.Properties;
		}
		catch (TaskCanceledException)
		{
			// Backing out of the browser. An ordinary answer rather than a failure - see the interface.
			return null;
		}
	}
}
