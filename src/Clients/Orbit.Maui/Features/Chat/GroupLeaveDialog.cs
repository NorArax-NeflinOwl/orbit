using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Chat;

namespace Orbit.Maui.Features.Chat;

/// <summary>
/// Puts a <see cref="GroupLeaveQuestion"/> on screen - for the group list and the group's own screen
/// alike, so the two cannot ask differently. The shape is the phone's own for a choice and for a
/// confirmation: an action sheet to pick who takes over, the way a stock-check order or a shelf is
/// picked (see TaskListDetailPage), then the same yes-or-no alert leaving has always asked.
/// </summary>
internal static class GroupLeaveDialog
{
	/// <summary>True once the reader has confirmed; who takes over, when asked, is left on the question.</summary>
	public static async Task<bool> AskAsync(Page page, GroupLeaveQuestion question, Translations translations)
	{
		if (question.MustChooseSuccessor && !await ChooseSuccessorAsync(page, question, translations))
		{
			return false;
		}

		return await page.DisplayAlertAsync(
			translations["Leave group"], question.Message, translations["Leave group"], translations["Cancel"]);
	}

	/// <summary>
	/// The server's own choice comes first and carries the tick, so the one that is chosen if nobody looks
	/// is the one marked - the same mark the stock-check order uses for the order in force.
	/// </summary>
	private static async Task<bool> ChooseSuccessorAsync(Page page, GroupLeaveQuestion question, Translations translations)
	{
		var labels = new Dictionary<string, Guid>();
		foreach (var candidate in question.Candidates)
		{
			var label = candidate.UserId == question.ChosenSuccessorUserId
				? $"{candidate.DisplayName} ✓"
				: candidate.DisplayName;

			// Two members can share a name ("Someone" stands in for anybody the phone cannot name), and an
			// action sheet answers with the label - so a repeated one is told apart rather than lost.
			var unique = label;
			for (var repeat = 2; labels.ContainsKey(unique); repeat++)
			{
				unique = $"{label} ({repeat})";
			}

			labels[unique] = candidate.UserId;
		}

		var chosen = await page.DisplayActionSheetAsync(
			question.SuccessorQuestion, translations["Cancel"], destruction: null, [.. labels.Keys]);

		if (chosen is null || !labels.TryGetValue(chosen, out var userId))
		{
			return false;
		}

		question.ChosenSuccessorUserId = userId;
		return true;
	}
}
