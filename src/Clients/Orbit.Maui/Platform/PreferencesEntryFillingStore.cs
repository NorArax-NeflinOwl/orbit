using Orbit.Mobile.Screens.Suggestions;

namespace Orbit.Maui.Platform;

/// <inheritdoc cref="PreferencesDashboardPinStore"/>
public sealed class PreferencesEntryFillingStore : IEntryFillingStore
{
	private const string KindsKey = "orbit.suggestions.filled-kinds";

	private readonly IPreferences _preferences;

	public PreferencesEntryFillingStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>Null while nothing is stored, which is every kind; an empty string is every one switched off.</summary>
	public IReadOnlySet<string>? Read()
		=> _preferences.ContainsKey(KindsKey)
			? _preferences.Get(KindsKey, string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet()
			: null;

	public void Write(IReadOnlySet<string> kinds)
		=> _preferences.Set(KindsKey, string.Join(',', EntryFilling.EntryKinds.Where(kinds.Contains)));
}
