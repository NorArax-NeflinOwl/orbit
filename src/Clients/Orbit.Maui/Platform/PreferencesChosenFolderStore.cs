using Orbit.Core.Folders;
using Orbit.Mobile.Screens.Folders;

namespace Orbit.Maui.Platform;

/// <summary>
/// Keeps which folder each list screen was left under - see <see cref="IChosenFolderStore"/>, and
/// <see cref="PreferencesListArrangementStore"/>, which keeps the other half of the same answer.
/// </summary>
public sealed class PreferencesChosenFolderStore : IChosenFolderStore
{
	private readonly IPreferences _preferences;

	public PreferencesChosenFolderStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>
	/// A folder somebody made is remembered by its id, and a built-in one by its name. Anything that
	/// does not read as either - a value written by a build that offered a different set, an id whose
	/// folder has since gone - reads as Public, which is where a screen opens anyway.
	/// </summary>
	public FolderKey Read(FolderPage page)
	{
		var stored = _preferences.Get<string?>(Key(page), null);

		if (Enum.TryParse<BuiltInFolder>(stored, out var builtIn))
		{
			return FolderKey.Of(builtIn);
		}

		return Guid.TryParse(stored, out var folderId) ? FolderKey.Of(folderId) : FolderKey.Default;
	}

	public void Write(FolderPage page, FolderKey chosen)
		=> _preferences.Set(
			Key(page),
			chosen.BuiltIn is { } builtIn ? builtIn.ToString() : chosen.FolderId?.ToString());

	private static string Key(FolderPage page) => $"orbit.list.{page}.folder";
}
