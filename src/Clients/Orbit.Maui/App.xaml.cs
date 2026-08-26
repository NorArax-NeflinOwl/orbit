namespace Orbit.Maui;

public partial class App : Application
{
	private readonly IServiceProvider _services;

	public App(IServiceProvider services)
	{
		InitializeComponent();
		_services = services;
	}

	/// <summary>
	/// Startup always begins at the version gate - nothing else may run before the app knows it is still
	/// allowed to. See <see cref="Features.Startup.StartupViewModel"/>.
	/// </summary>
	protected override Window CreateWindow(IActivationState? activationState)
		=> new(_services.GetRequiredService<Features.Startup.StartupPage>());
}
