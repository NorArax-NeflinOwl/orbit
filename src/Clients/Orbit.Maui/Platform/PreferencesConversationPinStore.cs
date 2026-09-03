using Orbit.Mobile.Screens.Chat;

namespace Orbit.Maui.Platform;

/// <inheritdoc cref="PreferencesDashboardPinStore"/>
public sealed class PreferencesConversationPinStore : IConversationPinStore
{
	private const string PinnedKey = "orbit.conversations.pinned";

	private readonly IPreferences _preferences;

	public PreferencesConversationPinStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>Anything that is not an id is dropped rather than throwing - see the dashboard's store.</summary>
	public IReadOnlySet<Guid> Read()
		=> (_preferences.Get<string?>(PinnedKey, null) ?? string.Empty)
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(stored => Guid.TryParse(stored, out var id) ? id : (Guid?)null)
			.Where(id => id is not null)
			.Select(id => id!.Value)
			.ToHashSet();

	public void Write(IReadOnlySet<Guid> pinned)
		=> _preferences.Set(PinnedKey, string.Join(',', pinned));
}
