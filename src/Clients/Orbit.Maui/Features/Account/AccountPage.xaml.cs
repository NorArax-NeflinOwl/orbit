using Orbit.Mobile.Screens.Account;

namespace Orbit.Maui.Features.Account;

public partial class AccountPage : ContentPage
{
	private readonly AccountViewModel _viewModel;

	public AccountPage(AccountViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_viewModel.ThemeChanged += (_, theme) => Apply(theme);
		_viewModel.ExportReady += (_, export) => _ = OfferAsync(export.FileName, export.Json);
		Apply(_viewModel.Theme);
	}

	/// <summary>
	/// Writes the export somewhere the share sheet can reach and hands it over. The cache directory
	/// rather than anywhere permanent: the file is on its way out, and whatever the reader saves it into
	/// is where it actually lives.
	/// </summary>
	private async Task OfferAsync(string fileName, string json)
	{
		var path = Path.Combine(FileSystem.CacheDirectory, fileName);
		await File.WriteAllTextAsync(path, json);
		await Share.Default.RequestAsync(new ShareFileRequest(fileName, new ShareFile(path)));
	}

	/// <summary>
	/// Picking the file is the platform's job, and reading it is the view model's - see
	/// AccountViewModel.ImportAsync.
	/// </summary>
	private async void OnImportClicked(object? sender, EventArgs e)
	{
		var picked = await FilePicker.Default.PickAsync(new PickOptions
		{
			PickerTitle = _viewModel.ImportPickerTitle,
			FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
			{
				[DevicePlatform.iOS] = ["public.json", "public.text"],
				[DevicePlatform.Android] = ["application/json"]
			})
		});

		if (picked is null)
		{
			return;
		}

		await using var stream = await picked.OpenReadAsync();
		using var reader = new StreamReader(stream);
		await _viewModel.ImportAsync(await reader.ReadToEndAsync());
	}

	/// <summary>The tab strip's buttons need the screen's command, and RelativeSource walks the visual tree.</summary>
	public AccountViewModel ViewModel => _viewModel;

	/// <summary>
	/// Turns the reader's choice into the app's theme. Unspecified means "follow the phone", which is
	/// what Orbit did before there was a choice at all.
	/// </summary>
	private static void Apply(ChosenTheme theme)
	{
		if (Application.Current is { } application)
		{
			application.UserAppTheme = theme switch
			{
				ChosenTheme.Light => AppTheme.Light,
				ChosenTheme.Dark => AppTheme.Dark,
				_ => AppTheme.Unspecified
			};
		}
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}
