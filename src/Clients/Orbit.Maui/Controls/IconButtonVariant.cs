namespace Orbit.Maui.Controls;

/// <summary>
/// Which icon button this one is. The two differ in exactly one way - whether they are drawn with an
/// edge - so they are one control asked which it is, rather than two.
///
/// There were four. The other two were the editing rail's Save and Cancel at twice this size, and the
/// rail is gone: the design has no bar along the foot, and what it held is now the bar's back arrow,
/// the menu under the screen's name and, on the one screen that saves, a floating button.
/// </summary>
public enum IconButtonVariant
{
	/// <summary>.icon-btn: 30 across, no edge. The collapse arrow on a card, a menu's three dots.</summary>
	Plain,

	/// <summary>.page-add: the same size, outlined in the accent. The plus every list screen opens with.</summary>
	Add
}
