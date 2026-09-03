using System.Windows.Input;

namespace Orbit.Maui.Controls;

/// <summary>
/// The name of a thing and what it is for, as one field - see the markup, and Orbit.Web's
/// TitledDescription for the same field on the other client.
/// </summary>
public partial class TitledDescription : ContentView
{
	public static readonly BindableProperty TitleProperty = BindableProperty.Create(
		nameof(Title), typeof(string), typeof(TitledDescription), string.Empty, BindingMode.TwoWay);

	public static readonly BindableProperty TitlePlaceholderProperty = BindableProperty.Create(
		nameof(TitlePlaceholder), typeof(string), typeof(TitledDescription), string.Empty);

	/// <summary>What the title box is called to a screen reader - "Title" for a list, "Name" for a warehouse.</summary>
	public static readonly BindableProperty TitleLabelProperty = BindableProperty.Create(
		nameof(TitleLabel), typeof(string), typeof(TitledDescription), string.Empty,
		propertyChanged: (field, _, value) =>
			SemanticProperties.SetDescription(((TitledDescription)field).TitleEntry, value as string ?? string.Empty));

	/// <summary>Saving the title, which the return key is for - the description has no return key to press.</summary>
	public static readonly BindableProperty SaveTitleCommandProperty = BindableProperty.Create(
		nameof(SaveTitleCommand), typeof(ICommand), typeof(TitledDescription));

	public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
		nameof(Description), typeof(string), typeof(TitledDescription), string.Empty, BindingMode.TwoWay);

	/// <summary>
	/// Saving the description, run when the reader leaves the box: an editor has no "done" key, so
	/// without this the first thing typed here was lost on leaving the screen.
	/// </summary>
	public static readonly BindableProperty CommitDescriptionCommandProperty = BindableProperty.Create(
		nameof(CommitDescriptionCommand), typeof(ICommand), typeof(TitledDescription));

	public static readonly BindableProperty IsReadOnlyProperty = BindableProperty.Create(
		nameof(IsReadOnly), typeof(bool), typeof(TitledDescription), false);

	/// <summary>
	/// False where there is nothing to describe to anybody: a private list is kept by nobody but its
	/// owner, and the server holds no description for one.
	/// </summary>
	public static readonly BindableProperty ShowsDescriptionProperty = BindableProperty.Create(
		nameof(ShowsDescription), typeof(bool), typeof(TitledDescription), true);

	/// <summary>Names already in use, offered as this one is typed.</summary>
	public static readonly BindableProperty SuggestionsProperty = BindableProperty.Create(
		nameof(Suggestions), typeof(View), typeof(TitledDescription),
		propertyChanged: (field, _, value) => Slot.Fill(((TitledDescription)field).SuggestionsHost, value));

	/// <summary>What can be done to the thing itself, where the screen offers anything.</summary>
	public static readonly BindableProperty MenuProperty = BindableProperty.Create(
		nameof(Menu), typeof(View), typeof(TitledDescription),
		propertyChanged: (field, _, value) => Slot.Fill(((TitledDescription)field).MenuHost, value));

	public TitledDescription()
	{
		InitializeComponent();
		DescriptionEditor.Unfocused += (_, _) => CommitDescriptionCommand?.Execute(null);
	}

	public string Title
	{
		get => (string)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	public string TitlePlaceholder
	{
		get => (string)GetValue(TitlePlaceholderProperty);
		set => SetValue(TitlePlaceholderProperty, value);
	}

	public string TitleLabel
	{
		get => (string)GetValue(TitleLabelProperty);
		set => SetValue(TitleLabelProperty, value);
	}

	public ICommand? SaveTitleCommand
	{
		get => (ICommand?)GetValue(SaveTitleCommandProperty);
		set => SetValue(SaveTitleCommandProperty, value);
	}

	public string Description
	{
		get => (string)GetValue(DescriptionProperty);
		set => SetValue(DescriptionProperty, value);
	}

	public ICommand? CommitDescriptionCommand
	{
		get => (ICommand?)GetValue(CommitDescriptionCommandProperty);
		set => SetValue(CommitDescriptionCommandProperty, value);
	}

	public bool IsReadOnly
	{
		get => (bool)GetValue(IsReadOnlyProperty);
		set => SetValue(IsReadOnlyProperty, value);
	}

	public bool ShowsDescription
	{
		get => (bool)GetValue(ShowsDescriptionProperty);
		set => SetValue(ShowsDescriptionProperty, value);
	}

	public View? Suggestions
	{
		get => (View?)GetValue(SuggestionsProperty);
		set => SetValue(SuggestionsProperty, value);
	}

	public View? Menu
	{
		get => (View?)GetValue(MenuProperty);
		set => SetValue(MenuProperty, value);
	}
}
