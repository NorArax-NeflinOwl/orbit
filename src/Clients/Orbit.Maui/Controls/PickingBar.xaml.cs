using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Folders;

namespace Orbit.Maui.Controls;

/// <summary>
/// The bar a list screen shows while several of its rows are being chosen - see PickingSeveral, which
/// holds everything the bar does. What is left here is asking: which folder, which contact and how much
/// they may do, each a sheet, because a question is the page's business and the view model has nothing
/// to ask with.
/// </summary>
public partial class PickingBar : ContentView
{
	public PickingBar() => InitializeComponent();

	private PickingSeveral? Picking => BindingContext as PickingSeveral;

	private static Translations Translations
		=> IPlatformApplication.Current!.Services.GetRequiredService<Translations>();

	private async void OnFileClicked(object? sender, EventArgs e)
	{
		if (Picking is not { } picking || PageOf(this) is not { } page)
		{
			return;
		}

		var noFolder = Translations["No folder"];
		var folders = picking.Folders;
		var chosen = await page.DisplayActionSheetAsync(
			Translations["Move to folder"], Translations["Cancel"], destruction: null,
			[noFolder, .. folders.Select(folder => folder.Name)]);

		if (chosen == noFolder)
		{
			picking.FileCommand.Execute(null);
		}
		else if (folders.FirstOrDefault(folder => folder.Name == chosen) is { } folder)
		{
			picking.FileCommand.Execute(folder.LocalId);
		}
	}

	private async void OnShareClicked(object? sender, EventArgs e)
	{
		if (Picking is not { } picking || PageOf(this) is not { } page)
		{
			return;
		}

		var contacts = await picking.ContactsAsync();
		if (contacts.Count == 0)
		{
			return;
		}

		// Two people with the same name would be one entry in a sheet, which picks by label - numbered
		// apart, as GroupLeaveDialog does it.
		var labels = new Dictionary<string, Orbit.Mobile.Data.LocalContact>();
		foreach (var contact in contacts)
		{
			var label = contact.DisplayName;
			for (var number = 2; labels.ContainsKey(label); number++)
			{
				label = $"{contact.DisplayName} ({number})";
			}

			labels[label] = contact;
		}

		var who = await page.DisplayActionSheetAsync(
			Translations["Share with"], Translations["Cancel"], destruction: null, [.. labels.Keys]);
		if (who is null || !labels.TryGetValue(who, out var recipient))
		{
			return;
		}

		var levels = picking.AccessLevels;
		var how = await page.DisplayActionSheetAsync(
			Translations["What they may do"], Translations["Cancel"], destruction: null,
			[.. levels.Select(level => level.Name)]);
		if (levels.FirstOrDefault(level => level.Name == how) is not { } accessLevel)
		{
			return;
		}

		await picking.ShareAsync(recipient, accessLevel.Value);
	}

	private static Page? PageOf(Element element)
	{
		for (var current = element.Parent; current is not null; current = current.Parent)
		{
			if (current is Page page)
			{
				return page;
			}
		}

		return null;
	}
}
