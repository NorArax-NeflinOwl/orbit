using Orbit.Core.Mobile;
using Orbit.Mobile.Authentication;

namespace Orbit.Maui.Platform;

/// <summary>
/// Sends the reader to Google in a system browser sheet and waits for iOS to hand the answer back on
/// the app's own URL scheme.
///
/// One thing has to be in the bundle before this can work, and it cannot be invented here: the iOS
/// OAuth client id, reversed, registered under <c>CFBundleURLTypes</c> in Info.plist, so iOS knows to
/// hand the redirect back to Orbit. The id itself comes from the server rather than the bundle - see
/// AuthenticationClient.GoogleClientIdAsync - so a deployment with no iOS client configured shows no
/// Google button and this is never reached.
/// </summary>
public sealed class WebSignInBrowser : IWebSignInBrowser
{
	public string Platform => nameof(MobilePlatform.Ios);

	/// <summary>
	/// Google's documented redirect for an iOS app: the client id with its dot-separated parts reversed,
	/// which is also the URL scheme the bundle must claim.
	/// </summary>
	public Uri CallbackAddressFor(string clientId)
		=> new($"{string.Join('.', clientId.Split('.').Reverse())}:/oauth2redirect");

	public async Task<IReadOnlyDictionary<string, string>?> SignInAsync(
		Uri startAddress, CancellationToken cancellationToken = default)
	{
		try
		{
			var result = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
			{
				Url = startAddress,
				CallbackUrl = CallbackFrom(startAddress),
				// Nothing here should be signed in on the reader's behalf by a cookie they forgot about.
				PrefersEphemeralWebBrowserSession = true
			});

			return result.Properties;
		}
		catch (TaskCanceledException)
		{
			// The sheet was dismissed. An answer, not a fault - see the interface.
			return null;
		}
	}

	/// <summary>
	/// The redirect the authorization address already carries, rather than a second computation of it
	/// from a client id this method is not given: the two have to agree exactly or the sheet never comes
	/// back, and taking it from the address is the only way they cannot drift.
	/// </summary>
	private static Uri CallbackFrom(Uri startAddress)
		=> new(System.Web.HttpUtility.ParseQueryString(startAddress.Query)["redirect_uri"]!);
}
