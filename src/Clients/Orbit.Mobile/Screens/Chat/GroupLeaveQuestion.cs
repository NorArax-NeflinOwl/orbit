using Orbit.Core.Chat.Groups;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Chat;

/// <summary>
/// What is asked before somebody leaves a group - from the group's own screen and from the group list
/// alike, and the same question Orbit.Web's GroupLeaveConfirmation asks, in the same words.
///
/// Everybody is asked to confirm, since leaving takes their copies of the group's messages with them and
/// only an admin can bring them back. The last person out is told the group goes with them. And the one
/// reader who would leave people behind with nobody to manage the group - its only admin - is asked who
/// takes over, with the person the server would pick anyway already chosen, so confirming without
/// looking is never worse than the automatic rule. Built here rather than on each screen so the two
/// cannot ask different questions, which is how the phone came to leave without asking at all.
///
/// Held by the view model and answered by the page, which is what knows how to put a question on
/// screen; the answer comes back through <see cref="ChosenSuccessorUserId"/>.
/// </summary>
public sealed class GroupLeaveQuestion
{
    private const string AdminRole = "Admin";

    private GroupLeaveQuestion(
        Guid groupId, string sentence, string? lastOneOutNote, string successorQuestion,
        IReadOnlyList<GroupSuccessorCandidate> candidates)
    {
        GroupId = groupId;
        Sentence = sentence;
        LastOneOutNote = lastOneOutNote;
        SuccessorQuestion = successorQuestion;
        Candidates = candidates;
        ChosenSuccessorUserId = candidates.Count > 0 ? candidates[0].UserId : null;
    }

    public Guid GroupId { get; }

    /// <summary>The confirmation everybody is asked, already in the reader's language.</summary>
    public string Sentence { get; }

    /// <summary>Said to the last person out, and to nobody else. Null otherwise.</summary>
    public string? LastOneOutNote { get; }

    public bool IsLastOneOut => LastOneOutNote is not null;

    /// <summary>The whole of what a confirmation should say - the sentence, and the note when there is one.</summary>
    public string Message => LastOneOutNote is { } note ? $"{Sentence}\n\n{note}" : Sentence;

    /// <summary>The heading of the choice, asked only when <see cref="MustChooseSuccessor"/>.</summary>
    public string SuccessorQuestion { get; }

    /// <summary>
    /// Who could take over, the person the server would choose first - see ChatGroup.ChooseSuccessor -
    /// and the rest by name. Empty unless the reader has to choose.
    /// </summary>
    public IReadOnlyList<GroupSuccessorCandidate> Candidates { get; }

    /// <summary>
    /// Whether this reader going would leave people in the group with no admin among them - the one case
    /// the server has to hand the group to somebody, and so the one case worth asking who.
    /// </summary>
    public bool MustChooseSuccessor => Candidates.Count > 0;

    /// <summary>Who takes over. Starts on the server's own choice, so leaving it alone changes nothing.</summary>
    public Guid? ChosenSuccessorUserId { get; set; }

    /// <summary>What to send as successorUserId: the choice when there was one to make, and nothing otherwise.</summary>
    public Guid? SuccessorToSend => MustChooseSuccessor ? ChosenSuccessorUserId : null;

    public static GroupLeaveQuestion For(LocalChatGroup group, Guid ownUserId, Translations translations)
    {
        var others = group.Members.Where(member => member.UserId != ownUserId).ToList();
        var mustChooseSuccessor = group.OwnRole == AdminRole
            && others.Count > 0
            && others.All(member => member.Role != AdminRole);

        return new GroupLeaveQuestion(
            group.Id,
            translations["Leave this group? Your copies of its messages go with you, and only an admin can add you back."],
            others.Count == 0 ? translations["You're the last one here, so the group is deleted when you go."] : null,
            translations["You're the only admin here. Who takes over?"],
            mustChooseSuccessor ? CandidatesFrom(others) : []);
    }

    /// <summary>
    /// The server's own choice first, by the very rule it uses, so the default and the automatic choice
    /// cannot disagree; everybody else after it in the order a reader looks for a name.
    /// </summary>
    private static List<GroupSuccessorCandidate> CandidatesFrom(IReadOnlyList<LocalChatGroupMember> others)
    {
        var longestStanding = ChatGroup.ChooseSuccessor(others, member => member.JoinedAtUtc, member => member.UserId);

        return
        [
            new GroupSuccessorCandidate(longestStanding.UserId, longestStanding.DisplayName),
            .. others
                .Where(member => member.UserId != longestStanding.UserId)
                .OrderBy(member => member.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(member => new GroupSuccessorCandidate(member.UserId, member.DisplayName))
        ];
    }
}

/// <summary>Somebody who could take a group over, as the choice names them.</summary>
public sealed record GroupSuccessorCandidate(Guid UserId, string DisplayName);
