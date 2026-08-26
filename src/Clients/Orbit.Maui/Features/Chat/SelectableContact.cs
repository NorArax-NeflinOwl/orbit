using CommunityToolkit.Mvvm.ComponentModel;
using Orbit.Mobile.Data;

namespace Orbit.Maui.Features.Chat;

/// <summary>
/// A contact with a tick next to it, for choosing who a new group holds. A wrapper rather than
/// CollectionView's own multiple selection because the choice has to survive the list being rebuilt,
/// and because a checkbox says plainly what tapping a row does.
/// </summary>
public sealed partial class SelectableContact : ObservableObject
{
	[ObservableProperty]
	private bool _isSelected;

	public SelectableContact(LocalContact contact) => Contact = contact;

	public LocalContact Contact { get; }

	public string DisplayName => Contact.DisplayName;
}
