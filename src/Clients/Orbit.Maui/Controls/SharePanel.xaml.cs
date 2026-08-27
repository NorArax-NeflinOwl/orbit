namespace Orbit.Maui.Controls;

/// <summary>
/// The share panel's markup. It has no code of its own: everything it does lives on
/// <see cref="Orbit.Mobile.Screens.Sharing.SharePanel"/>, which each editor hands it as a binding
/// context - so the four editors share one panel rather than four copies of the same form.
/// </summary>
public partial class SharePanel : ContentView
{
	public SharePanel() => InitializeComponent();
}
