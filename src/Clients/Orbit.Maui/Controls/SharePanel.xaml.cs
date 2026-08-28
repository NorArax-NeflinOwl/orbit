using Panel = Orbit.Mobile.Screens.Sharing.SharePanel;

namespace Orbit.Maui.Controls;

/// <summary>
/// The share panel's markup. Almost everything it does lives on
/// <see cref="Orbit.Mobile.Screens.Sharing.SharePanel"/>, which each editor hands it as a binding
/// context - so the four editors share one panel rather than four copies of the same form.
///
/// The exception is handing a link somewhere: that is the system share sheet, a platform call, and
/// keeping it here is what lets the panel itself be tested without one.
/// </summary>
public partial class SharePanel : ContentView
{
	public SharePanel()
	{
		InitializeComponent();
		BindingContextChanged += (_, _) => Listen();
	}

	private Panel? _listeningTo;

	private void Listen()
	{
		if (_listeningTo is { } previous)
		{
			previous.LinkReady -= OnLinkReady;
		}

		_listeningTo = BindingContext as Panel;
		if (_listeningTo is { } panel)
		{
			panel.LinkReady += OnLinkReady;
		}
	}

	private static void OnLinkReady(object? sender, string address)
		=> _ = Share.Default.RequestAsync(new ShareTextRequest(address)
		{
			Title = address
		});
}
