using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;

namespace Orbit.Maui.Controls;

/// <summary>
/// The words the sorting and filtering menus are written with, in the order they are offered.
///
/// Here rather than on each screen because Notes and Tasks offer the same two menus - the design draws
/// both list screens the same way, and a reader who has learnt one has learnt the other. Two copies of
/// the same five words is two chances for them to drift.
/// </summary>
public static class ListMenus
{
	public static IReadOnlyList<(ListSortOrder Value, string Name)> SortOrders(Translations translations) =>
	[
		(ListSortOrder.Recent, translations["Last changed"]),
		(ListSortOrder.Name, translations["Name"]),
		(ListSortOrder.Priority, translations["Priority"])
	];

	public static IReadOnlyList<(ListFilter Value, string Name)> Filters(Translations translations) =>
	[
		(ListFilter.All, translations["Everything"]),
		(ListFilter.Pinned, translations["Pinned"]),
		(ListFilter.HighPriority, translations["High priority"]),
		(ListFilter.NormalPriority, translations["Normal priority"]),
		(ListFilter.LowPriority, translations["Low priority"])
	];
}
