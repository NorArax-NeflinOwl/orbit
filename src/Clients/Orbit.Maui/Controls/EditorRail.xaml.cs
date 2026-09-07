using System.Windows.Input;
using Orbit.Mobile.Localization;

namespace Orbit.Maui.Controls;

/// <summary>
/// The bar along the foot of an editing or reading screen - see the markup, and Orbit.Web's
/// Components/EditorRail.razor, which this is the phone's half of.
/// </summary>
public partial class EditorRail : ContentView
{
	/// <summary>
	/// What Save writes. Left unset by a screen that is read rather than filled in, and the button is
	/// then not drawn at all - a Save that does nothing is worse than no Save.
	/// </summary>
	public static readonly BindableProperty SaveCommandProperty = BindableProperty.Create(
		nameof(SaveCommand), typeof(ICommand), typeof(EditorRail),
		propertyChanged: (rail, _, value) =>
		{
			var bar = (EditorRail)rail;
			bar.SaveAction.Command = value as ICommand;
			bar.SaveAction.IsVisible = value is not null;
		});

	/// <summary>Nothing written yet, so there is nothing to write down.</summary>
	public static readonly BindableProperty CanSaveProperty = BindableProperty.Create(
		nameof(CanSave), typeof(bool), typeof(EditorRail), true,
		propertyChanged: (rail, _, value) => ((EditorRail)rail).SaveAction.IsEnabledForPress = value is true);

	/// <summary>Leaving the screen: out of the form, or back to the list on one that is only read.</summary>
	public static readonly BindableProperty CancelCommandProperty = BindableProperty.Create(
		nameof(CancelCommand), typeof(ICommand), typeof(EditorRail),
		propertyChanged: (rail, _, value) => ((EditorRail)rail).CancelAction.Command = value as ICommand);

	/// <summary>
	/// What this screen keeps in view whatever the form is scrolled to - a lock's explanation, the
	/// sentence saying why a save was refused. Most screens have none.
	/// </summary>
	public static readonly BindableProperty ExtrasProperty = BindableProperty.Create(
		nameof(Extras), typeof(View), typeof(EditorRail),
		propertyChanged: (rail, _, value) => ((EditorRail)rail).Hold(value as View));

	/// <summary>Anything else that belongs beside the two actions - the screen's own overflow menu.</summary>
	public static readonly BindableProperty ChildProperty = BindableProperty.Create(
		nameof(Child), typeof(View), typeof(EditorRail),
		propertyChanged: (rail, _, value) => Slot.Fill(((EditorRail)rail).ChildHost, value));

	private bool _isOpen;

	/// <summary>
	/// Watched rather than only held: a screen hands this over once and lets it hide itself - a note's
	/// read-only reason is a Label that appears when the note turns out to be somebody else's. The
	/// arrow has to come and go with it, or every screen carrying the slot draws an arrow that opens an
	/// empty line.
	/// </summary>
	private View? _extras;

	public EditorRail()
	{
		InitializeComponent();

		var translations = IPlatformApplication.Current?.Services.GetService<Translations>();
		SaveAction.Description = translations?["Save"] ?? "Save";
		CancelAction.Description = translations?["Cancel"] ?? "Cancel";
		Toggle.Command = new Command(() => Fold(!_isOpen));
		Fold(false);
	}

	public ICommand? SaveCommand
	{
		get => (ICommand?)GetValue(SaveCommandProperty);
		set => SetValue(SaveCommandProperty, value);
	}

	/// <inheritdoc cref="CanSaveProperty"/>
	public bool CanSave
	{
		get => (bool)GetValue(CanSaveProperty);
		set => SetValue(CanSaveProperty, value);
	}

	public ICommand? CancelCommand
	{
		get => (ICommand?)GetValue(CancelCommandProperty);
		set => SetValue(CancelCommandProperty, value);
	}

	/// <inheritdoc cref="ExtrasProperty"/>
	public View? Extras
	{
		get => (View?)GetValue(ExtrasProperty);
		set => SetValue(ExtrasProperty, value);
	}

	/// <inheritdoc cref="ChildProperty"/>
	public View? Child
	{
		get => (View?)GetValue(ChildProperty);
		set => SetValue(ChildProperty, value);
	}

	private void Hold(View? extras)
	{
		if (_extras is not null)
		{
			_extras.PropertyChanged -= OnExtrasChanged;
		}

		_extras = extras;
		ExtrasHost.Content = extras;

		if (extras is not null)
		{
			extras.PropertyChanged += OnExtrasChanged;
		}

		Fold(false);
	}

	private void OnExtrasChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
	{
		if (args.PropertyName == nameof(IsVisible))
		{
			Fold(_isOpen);
		}
	}

	private void Fold(bool open)
	{
		// The arrow is drawn only where there is something behind it to unfold: one that opens an empty
		// line is a control that does nothing.
		var hasSomethingToSay = _extras is { IsVisible: true };
		Toggle.IsVisible = hasSomethingToSay;

		_isOpen = open && hasSomethingToSay;
		ExtrasHost.IsVisible = _isOpen;

		var translations = IPlatformApplication.Current?.Services.GetService<Translations>();
		Toggle.Description = _isOpen ? translations?["Minimise"] ?? "Minimise" : translations?["Expand"] ?? "Expand";
		Toggle.Data = new Microsoft.Maui.Controls.Shapes.PathGeometryConverter()
			.ConvertFromInvariantString(_isOpen ? "M6,12 L10,8 L14,12" : "M6,8 L10,12 L14,8") as Microsoft.Maui.Controls.Shapes.Geometry;
	}
}
