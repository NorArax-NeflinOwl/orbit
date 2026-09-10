using Orbit.Core.Folders;
using Orbit.Mobile.Screens.Folders;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Which folder each list screen was left under, remembered for the life of one test - the device
/// preferences the app keeps it in, without a device. See <see cref="IChosenFolderStore"/>.
/// </summary>
public sealed class InMemoryChosenFolderStore : IChosenFolderStore
{
    private readonly Dictionary<FolderPage, FolderKey> _chosen = [];

    public FolderKey Read(FolderPage page) => _chosen.TryGetValue(page, out var key) ? key : FolderKey.Default;

    public void Write(FolderPage page, FolderKey chosen) => _chosen[page] = chosen;
}
