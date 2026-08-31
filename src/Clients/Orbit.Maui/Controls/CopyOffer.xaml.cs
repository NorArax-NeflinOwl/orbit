using System.Windows.Input;

namespace Orbit.Maui.Controls;

/// <summary>
/// "Then take a copy?" - the offer every one of the four detail screens makes when the offline policy
/// has just refused an edit. One control rather than four blocks of near-identical XAML, for the same
/// reason PinButton is one: the offer means the same thing wherever somebody meets it, and four copies
/// of it would drift apart a word at a time.
/// </summary>
public partial class CopyOffer : ContentView
{
	public static readonly BindableProperty IsOfferedProperty =
		BindableProperty.Create(nameof(IsOffered), typeof(bool), typeof(CopyOffer), false);

	public static readonly BindableProperty TakeCommandProperty =
		BindableProperty.Create(nameof(TakeCommand), typeof(ICommand), typeof(CopyOffer));

	public static readonly BindableProperty DeclineCommandProperty =
		BindableProperty.Create(nameof(DeclineCommand), typeof(ICommand), typeof(CopyOffer));

	public CopyOffer() => InitializeComponent();

	public bool IsOffered
	{
		get => (bool)GetValue(IsOfferedProperty);
		set => SetValue(IsOfferedProperty, value);
	}

	/// <summary>Taking the copy, which also opens it - see NoteDetailViewModel.CopyForEditingAsync.</summary>
	public ICommand? TakeCommand
	{
		get => (ICommand?)GetValue(TakeCommandProperty);
		set => SetValue(TakeCommandProperty, value);
	}

	/// <summary>Reading it and leaving it alone, which is the ordinary answer - and asked only once.</summary>
	public ICommand? DeclineCommand
	{
		get => (ICommand?)GetValue(DeclineCommandProperty);
		set => SetValue(DeclineCommandProperty, value);
	}
}
