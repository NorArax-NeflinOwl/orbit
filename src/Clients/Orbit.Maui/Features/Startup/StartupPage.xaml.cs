namespace Orbit.Maui.Features.Startup;

public partial class StartupPage : ContentPage
{
	private readonly StartupViewModel _viewModel;

	public StartupPage(StartupViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		try
		{
			var decision = await _viewModel.DecideAsync();
			if (decision.StopsTheApp)
			{
				return;
			}

			if (decision.OffersUpdate && decision.UpdateUrl is not null)
			{
				await OfferUpdateAsync(decision);
			}
		}
		catch (Exception exception)
		{
			// Nothing raised here is worth stranding the user on a splash screen for - and because this
			// is an async void, letting it escape would take the app down instead. Failing open matches
			// the gate's own rule: block only on a verdict actually held.
			System.Diagnostics.Debug.WriteLine($"Startup check failed, continuing: {exception}");
		}

		await _viewModel.ContinueToAppAsync();
	}

	/// <summary>
	/// The dismissible half of the version gate: worth telling the user about, never worth stopping them
	/// for. Lives here rather than in the view model because only a page can raise it.
	/// </summary>
	private async Task OfferUpdateAsync(Orbit.Mobile.Update.VersionGateDecision decision)
	{
		var wantsUpdate = await DisplayAlertAsync(
			"Update available",
			$"Orbit {decision.LatestVersion} is available.",
			"Update",
			"Not now");

		if (wantsUpdate)
		{
			await Launcher.Default.OpenAsync(decision.UpdateUrl!);
		}
	}
}
