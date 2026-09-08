using System.Globalization;
using Orbit.Mobile.Screens;

namespace Orbit.Maui.Controls;

/// <summary>
/// The circle a person or a group is drawn as - see the anatomy in the markup, and Orbit.Web's
/// .avatar / .avatar-sm, which it is the same thing as.
/// </summary>
public partial class AvatarCircle : ContentView
{
	/// <summary>
	/// Whose circle this is. Decides the colour, which is theirs and stays theirs however the list
	/// around it is sorted or filtered.
	/// </summary>
	public static readonly BindableProperty IdProperty = BindableProperty.Create(
		nameof(Id), typeof(Guid), typeof(AvatarCircle), Guid.Empty, propertyChanged: OnWhoChanged);

	/// <summary>What they are called, which is where the initials come from.</summary>
	public static readonly BindableProperty NameProperty = BindableProperty.Create(
		nameof(Name), typeof(string), typeof(AvatarCircle), string.Empty, propertyChanged: OnWhoChanged);

	/// <summary>
	/// How far across. Orbit.Web draws two: 32 beside a name it is the subject of, 26 on a dashboard
	/// row, where the row is a glance rather than a place. The phone's own lists use a little more,
	/// because a circle is also something to press.
	/// </summary>
	public static readonly BindableProperty DiameterProperty = BindableProperty.Create(
		nameof(Diameter), typeof(double), typeof(AvatarCircle), 36.0, propertyChanged: OnSizeChanged);

	/// <summary>
	/// Where somebody is, by the server's PresenceStatus name. Empty draws no dot at all, which is what
	/// a group gets: a group is not somewhere anybody is or is not.
	/// </summary>
	public static readonly BindableProperty StatusProperty = BindableProperty.Create(
		nameof(Status), typeof(string), typeof(AvatarCircle), string.Empty, propertyChanged: OnStatusChanged);

	private readonly PresenceColorConverter _presenceColours = new();

	public AvatarCircle()
	{
		InitializeComponent();
		Resize();
	}

	/// <inheritdoc cref="IdProperty"/>
	public Guid Id
	{
		get => (Guid)GetValue(IdProperty);
		set => SetValue(IdProperty, value);
	}

	public string Name
	{
		get => (string)GetValue(NameProperty);
		set => SetValue(NameProperty, value);
	}

	/// <inheritdoc cref="DiameterProperty"/>
	public double Diameter
	{
		get => (double)GetValue(DiameterProperty);
		set => SetValue(DiameterProperty, value);
	}

	/// <inheritdoc cref="StatusProperty"/>
	public string Status
	{
		get => (string)GetValue(StatusProperty);
		set => SetValue(StatusProperty, value);
	}

	/// <summary>
	/// The name and the id arrive one after the other as a row is bound, and the circle is made of
	/// both, so it is drawn again whichever of them lands second.
	/// </summary>
	private static void OnWhoChanged(BindableObject bindable, object oldValue, object newValue)
	{
		var avatar = (AvatarCircle)bindable;
		var who = Avatar.Of(avatar.Id, avatar.Name);

		avatar.InitialsLabel.Text = who.Initials;

		// The hue the browser picks for this person, in the colour space this client draws in. Not the
		// same numbers as its oklch, but the same person is the same colour on the same screen, which
		// is what the colour is for.
		avatar.Circle.BackgroundColor = Color.FromHsla(who.Hue / 360.0, 0.5, 0.55);
	}

	private static void OnSizeChanged(BindableObject bindable, object oldValue, object newValue)
		=> ((AvatarCircle)bindable).Resize();

	/// <summary>
	/// Everything that follows from how far across it is. The initials are the browser's own two sizes
	/// rather than a fraction of the diameter: it draws 11 on the small circle and 13 on the larger one,
	/// and a formula fitted to two points is a rule nobody wrote.
	/// </summary>
	private void Resize()
	{
		Frame.WidthRequest = Diameter;
		Frame.HeightRequest = Diameter;
		Circle.WidthRequest = Diameter;
		Circle.HeightRequest = Diameter;
		Round.CornerRadius = Diameter / 2;
		InitialsLabel.FontSize = Diameter <= 28 ? 11 : 13;
	}

	private static void OnStatusChanged(BindableObject bindable, object oldValue, object newValue)
	{
		var avatar = (AvatarCircle)bindable;
		var status = newValue as string ?? string.Empty;

		avatar.PresenceDot.IsVisible = status.Length > 0;
		avatar.PresenceDot.BackgroundColor =
			(Color)avatar._presenceColours.Convert(status, typeof(Color), null, CultureInfo.CurrentCulture);
	}
}
