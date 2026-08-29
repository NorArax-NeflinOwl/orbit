namespace Orbit.Mobile.Update;

/// <summary>
/// Sends the reader to wherever the newer build is - the App Store or Play Store listing the server
/// named in its verdict (§7 of info/orbit-maui-plan.md).
///
/// Behind an interface because leaving the app is a platform call, and it is the only one the startup
/// screen makes. Going somewhere else in Orbit is <see cref="Screens.IScreenNavigator"/>'s job; this is
/// the opposite, and keeping them apart is what stops "navigate" meaning two different things.
/// </summary>
public interface IUpdateLink
{
    Task OpenAsync(string url);
}
