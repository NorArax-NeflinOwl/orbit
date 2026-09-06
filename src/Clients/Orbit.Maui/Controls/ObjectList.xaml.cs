namespace Orbit.Maui.Controls;

/// <summary>
/// The three-way gate every list screen opens with - see the markup, and Orbit.Web's ObjectList for
/// the same one on the other client. A list says one of three things and it should say each of them
/// the same way wherever it is read.
/// </summary>
[ContentProperty(nameof(Body))]
public partial class ObjectList : ContentView
{
	public static readonly BindableProperty IsLoadingProperty = BindableProperty.Create(
		nameof(IsLoading), typeof(bool), typeof(ObjectList), false,
		propertyChanged: (list, _, _) => ((ObjectList)list).Decide());

	public static readonly BindableProperty IsEmptyProperty = BindableProperty.Create(
		nameof(IsEmpty), typeof(bool), typeof(ObjectList), false,
		propertyChanged: (list, _, _) => ((ObjectList)list).Decide());

	/// <summary>What the screen says when there is nothing on it. Every list has its own wording.</summary>
	public static readonly BindableProperty EmptyMessageProperty = BindableProperty.Create(
		nameof(EmptyMessage), typeof(string), typeof(ObjectList), string.Empty);

	public static readonly BindableProperty BodyProperty = BindableProperty.Create(
		nameof(Body), typeof(View), typeof(ObjectList),
		propertyChanged: (list, _, value) => ((ObjectList)list).Hold(value as View));

	public ObjectList()
	{
		InitializeComponent();
		Decide();
	}

	public bool IsLoading
	{
		get => (bool)GetValue(IsLoadingProperty);
		set => SetValue(IsLoadingProperty, value);
	}

	public bool IsEmpty
	{
		get => (bool)GetValue(IsEmptyProperty);
		set => SetValue(IsEmptyProperty, value);
	}

	/// <inheritdoc cref="EmptyMessageProperty"/>
	public string EmptyMessage
	{
		get => (string)GetValue(EmptyMessageProperty);
		set => SetValue(EmptyMessageProperty, value);
	}

	/// <summary>What there is to show, once there is any.</summary>
	public View? Body
	{
		get => (View?)GetValue(BodyProperty);
		set => SetValue(BodyProperty, value);
	}

	private void Hold(View? body)
	{
		BodyHost.Content = body;
		Decide();
	}

	private void Decide()
	{
		LoadingLabel.IsVisible = IsLoading;
		EmptyLabel.IsVisible = !IsLoading && IsEmpty;
		BodyHost.IsVisible = !IsLoading && !IsEmpty;
	}
}
