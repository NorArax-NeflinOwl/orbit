namespace Orbit.Maui.Controls;

/// <summary>
/// Which of Orbit.Web's three icon buttons this one is. They differ in exactly two ways - how big, and
/// whether they are drawn with an edge - so they are one control asked which it is, rather than three.
/// </summary>
public enum IconButtonVariant
{
	/// <summary>.icon-btn: 30 across, no edge. The collapse arrow on a card, a menu's three dots.</summary>
	Plain,

	/// <summary>.page-add: the same size, outlined in the accent. The plus every list screen opens with.</summary>
	Add,

	/// <summary>.icon-btn.page-action: 44 across with a hairline. Cancel on an editing screen's rail.</summary>
	Action,

	/// <summary>.page-action-primary: the same again, outlined in the accent. Save, and only Save.</summary>
	ActionPrimary
}
