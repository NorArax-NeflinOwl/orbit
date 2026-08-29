using Orbit.Core.Mobile;
using Orbit.Mobile.Authentication;

namespace Orbit.Maui.Platform;

/// <summary>
/// The iOS half of signing in with Google, and deliberately not built yet - it says so rather than
/// opening a browser that could only come back to nothing.
///
/// What it needs first: an iOS OAuth client in the same Google Cloud project (the Android one will not
/// do - a token carries the id of whichever client obtained it, see GoogleAuthSettings), that id in the
/// deployment's GoogleAuth:IosClientId, and its reversed form registered as a URL scheme in Info.plist
/// so the redirect reaches the app at all. The Android counterpart is the shape to follow.
///
/// Unreached until then: with no id configured the server answers with an empty one, and the sign-in
/// screen shows no Google button - see SignInViewModel.IsGoogleOffered.
/// </summary>
public sealed class WebSignInBrowser : IWebSignInBrowser
{
	public Uri CallbackAddress { get; } = new($"{AppInfo.PackageName}:/oauth2redirect");

	public string Platform => nameof(MobilePlatform.Ios);

	public Task<IReadOnlyDictionary<string, string>?> SignInAsync(
		Uri startAddress, CancellationToken cancellationToken = default)
		=> Task.FromResult<IReadOnlyDictionary<string, string>?>(null);
}
