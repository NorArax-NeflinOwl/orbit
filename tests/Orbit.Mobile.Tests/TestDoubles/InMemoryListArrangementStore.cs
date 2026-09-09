using Orbit.Mobile.Screens;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// How the list screens are being read, held for the length of one test rather than on the device.
/// Nothing remembered by default, which is a phone whose owner has never opened the menu.
/// </summary>
internal sealed class InMemoryListArrangementStore : IListArrangementStore
{
    private readonly Dictionary<ListSection, ListArrangement> _kept = [];

    public ListArrangement Read(ListSection section)
        => _kept.TryGetValue(section, out var kept) ? kept : ListArrangement.Default;

    public void Write(ListSection section, ListArrangement arrangement) => _kept[section] = arrangement;
}
