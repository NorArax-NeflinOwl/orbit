using Android.App;
using Android.Content;
using Android.Content.PM;

namespace Orbit.Maui;

/// <summary>
/// Where Google sends the reader back to. Does nothing itself: MAUI's base activity hands the callback
/// to whichever <see cref="Microsoft.Maui.Authentication.WebAuthenticator"/> call is waiting, and this
/// exists only so the scheme can be declared to Android.
///
/// The scheme has to be written out here because an intent filter is an attribute and takes a constant,
/// where <see cref="WebSignInBrowser"/> builds the same address from AppInfo.PackageName. Keep the two
/// in step with ApplicationId in Orbit.Maui.csproj: a mismatch means Google's redirect reaches nothing
/// and the flow simply never comes back.
///
/// NoHistory so the redirect leaves nothing behind a back gesture, and Exported because the party
/// starting it is the browser rather than Orbit.
/// </summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
	[Intent.ActionView],
	Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
	DataScheme = "com.orbitmaui.android")]
public class WebAuthenticationCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity;
