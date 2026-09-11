using Orbit.Core;
using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups;

/// <summary>
/// A named chat with more than two people in it. Membership and roles live here rather than in the
/// handlers so every rule about who may do what is stated once, in the place that can actually enforce
/// it - each method takes the id of whoever is asking and refuses if they aren't entitled.
///
/// The group owns no messages: a group message is fanned out into ordinary per-recipient rows so it
/// stays end-to-end encrypted under the pairwise keys people already have (see ChatMessage.CreateForGroup
/// and SendGroupMessageCommandHandler). What that costs is spelled out there.
/// </summary>
public sealed class ChatGroup
{
    private readonly List<ChatGroupMembership> _members;

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// When something last happened here - a message posted, or the group being made if none has been.
    /// Kept on the group and stamped as messages are sent, the same way a contact row carries the time
    /// of the last message with that person (see IContactRepository): the conversation list sorts people
    /// and groups against each other, and it can only do that if both answer the same question.
    ///
    /// Never null, so that list is totally ordered from the moment a group exists rather than needing a
    /// second rule for the ones nobody has written in yet.
    /// </summary>
    public DateTimeOffset LastMessageAtUtc { get; private set; }

    public IReadOnlyList<ChatGroupMembership> Members => _members;

    private ChatGroup(
        Guid id, string name, Guid createdByUserId, DateTimeOffset createdAtUtc, DateTimeOffset lastMessageAtUtc,
        List<ChatGroupMembership> members)
    {
        Id = id;
        Name = name;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
        LastMessageAtUtc = lastMessageAtUtc;
        _members = members;
    }

    /// <summary>The creator is the first admin - a group with nobody able to manage it would be stuck from the start.</summary>
    public static ChatGroup Create(Guid createdByUserId, string name)
    {
        StoredTextLimits.OrRefuse(name, StoredTextLimits.GroupName, "group's name");
        var trimmedName = name.Trim();
        if (trimmedName.Length == 0)
        {
            throw new InvalidRequestException("A group needs a name.");
        }

        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        return new ChatGroup(
            id, trimmedName, createdByUserId, now, now,
            [new ChatGroupMembership(id, createdByUserId, ChatGroupRole.Admin, now)]);
    }

    /// <summary>Rebuilds a group from already-persisted values, bypassing every rule below.</summary>
    public static ChatGroup FromPersistence(
        Guid id, string name, Guid createdByUserId, DateTimeOffset createdAtUtc, DateTimeOffset lastMessageAtUtc,
        IReadOnlyList<ChatGroupMembership> members)
        => new(id, name, createdByUserId, createdAtUtc, lastMessageAtUtc, [.. members]);

    /// <summary>
    /// Says a message has just been posted here, which is what the conversation list sorts on - see
    /// <see cref="LastMessageAtUtc"/>. Called where the fan-out is written, so the stamp and the
    /// messages it stands for land in the same operation.
    /// </summary>
    public void MarkMessagePosted() => LastMessageAtUtc = DateTimeOffset.UtcNow;

    public ChatGroupMembership? FindMember(Guid userId) => _members.FirstOrDefault(member => member.UserId == userId);

    public bool IsMember(Guid userId) => FindMember(userId) is not null;

    public bool IsAdmin(Guid userId) => FindMember(userId)?.Role == ChatGroupRole.Admin;

    public void Rename(Guid actorUserId, string name)
    {
        RequireAdmin(actorUserId);
        var trimmedName = name.Trim();
        if (trimmedName.Length == 0)
        {
            throw new InvalidRequestException("A group needs a name.");
        }

        Name = trimmedName;
    }

    /// <summary>Adding someone already in the group is a no-op rather than an error - the end state is what was asked for.</summary>
    public void AddMember(Guid actorUserId, Guid userId)
    {
        RequireAdmin(actorUserId);
        if (IsMember(userId))
        {
            return;
        }

        _members.Add(new ChatGroupMembership(Id, userId, ChatGroupRole.Member, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Takes somebody out of the group - or shows themselves out, which is not the same act and does not
    /// need the same standing. Removing another member is an admin's to do; leaving is anybody's, and
    /// requiring admin for both left an ordinary member with no way out of a group at all. Removing
    /// yourself is leaving, so it is handed to <see cref="Leave"/> and follows its rules - installed phone
    /// builds leave through this route, and a second set of rules for the same act is how the two drifted.
    ///
    /// Removing somebody else needs no "last admin" check: the actor is an admin and is not the one going,
    /// so an admin is left whoever the subject is.
    /// </summary>
    public void RemoveMember(Guid actorUserId, Guid userId)
    {
        if (actorUserId == userId)
        {
            Leave(actorUserId);
            return;
        }

        RequireAdmin(actorUserId);
        if (FindMember(userId) is { } member)
        {
            _members.Remove(member);
        }
    }

    /// <summary>
    /// Shows somebody out of the group. Anybody may leave, whatever their role and whoever else is in it:
    /// refusing the last admin until they had promoted somebody - which is what this used to do - left
    /// the one person with the most say in a group as the one person who could not walk out of it, and a
    /// phone that offered no "promote" step before "leave" could not get them out at all.
    ///
    /// What is still prevented is the state that refusal was protecting: a group with people in it and
    /// nobody able to manage it. So when the last admin goes, somebody takes over. The leaver may name
    /// who (successorUserId), which is what the web asks them before confirming; left unnamed, the group
    /// promotes the longest-standing member itself, by the same rule an account deletion uses - see
    /// <see cref="ChooseSuccessor{TMember}"/>.
    ///
    /// A named successor who is not in the group, or is the leaver, is refused rather than quietly
    /// replaced by the automatic choice. The leaver asked for a particular person; handing the group to
    /// somebody else instead is a decision they did not make and can no longer undo once they are out,
    /// whereas a refusal costs them one more look at the roster. Naming one is also an admin's act - it
    /// promotes somebody - so a plain member who names one is refused the same way.
    ///
    /// The last person out is let go, emptying the group - the same end <see cref="RemoveDeletedAccount"/>
    /// reaches, and refusing there would strand them instead.
    /// </summary>
    public void Leave(Guid userId, Guid? successorUserId = null)
    {
        var member = FindMember(userId)
            ?? throw new InvalidRequestException("You aren't in this group.");

        if (successorUserId is { } chosenUserId)
        {
            HandOver(member, chosenUserId);
        }

        _members.Remove(member);
        PromoteSuccessorIfNobodyManages();
    }

    /// <summary>
    /// Makes the leaver's chosen successor an admin before the leaver goes. Checked in full before
    /// anything changes, so a refused choice leaves the group exactly as it was.
    /// </summary>
    private void HandOver(ChatGroupMembership leaving, Guid successorUserId)
    {
        if (leaving.Role != ChatGroupRole.Admin)
        {
            throw new InvalidRequestException("Only a group admin can choose who takes over.");
        }

        if (successorUserId == leaving.UserId)
        {
            throw new InvalidRequestException("Choose somebody other than yourself to take over.");
        }

        var successor = FindMember(successorUserId)
            ?? throw new InvalidRequestException("The person you chose to take over isn't in this group any more.");

        if (successor.Role == ChatGroupRole.Admin)
        {
            return;
        }

        _members.Remove(successor);
        _members.Add(successor with { Role = ChatGroupRole.Admin });
    }

    /// <summary>
    /// Who takes a group over when its last admin goes without naming anybody: the longest-standing of
    /// the candidates, on the grounds that they have seen the most of the group. Public and generic so
    /// the web can offer the same person as the default in its "who takes over" picker, from the member
    /// list it holds, instead of a second copy of the rule that could drift from this one.
    /// </summary>
    public static TMember ChooseSuccessor<TMember>(
        IEnumerable<TMember> candidates, Func<TMember, DateTimeOffset> joinedAtUtc, Func<TMember, Guid> userId)
        => candidates
            .OrderBy(joinedAtUtc)
            // Two people can join in the same tick; ordering by id as well keeps the choice deterministic
            // rather than dependent on however the rows came back.
            .ThenBy(userId)
            .First();

    /// <summary>
    /// Promotes a successor when the group has people in it and no admin among them - which only happens
    /// the moment an admin has gone. Does nothing to an emptied group: there is nobody to promote, and
    /// the caller deletes it.
    /// </summary>
    private void PromoteSuccessorIfNobodyManages()
    {
        if (AdminCount > 0 || _members.Count == 0)
        {
            return;
        }

        var successor = ChooseSuccessor(_members, member => member.JoinedAtUtc, member => member.UserId);
        _members.Remove(successor);
        _members.Add(successor with { Role = ChatGroupRole.Admin });
    }

    /// <summary>
    /// Removes someone because their account was deleted, which is not the same thing as
    /// <see cref="RemoveMember"/> and deliberately breaks two of its rules.
    ///
    /// There is no actor: nobody performed this, the person simply no longer exists. And it cannot be
    /// refused - refusing would mean an account could not be deleted because of a group it happens to be
    /// in, which is not a trade anyone would accept. When the account was the last admin, the
    /// longest-standing remaining member takes over - the same succession <see cref="Leave"/> applies
    /// when nobody was named, since a deleted account can name nobody. Leaving it admin-less instead would
    /// strand a group nobody can add to, rename, or remove from - the state every other rule here exists
    /// to prevent.
    ///
    /// Leaves the group empty rather than deleting it, because a group is not this type's to delete -
    /// see DeleteAccountCommandHandler, which removes an emptied group once this has run.
    /// </summary>
    public void RemoveDeletedAccount(Guid userId)
    {
        var member = FindMember(userId);
        if (member is null)
        {
            return;
        }

        _members.Remove(member);
        PromoteSuccessorIfNobodyManages();
    }

    /// <summary>True once nobody is left - see <see cref="RemoveDeletedAccount"/>.</summary>
    public bool IsEmpty => _members.Count == 0;

    /// <summary>
    /// Promotes or demotes a member. Demoting the last admin is refused: it would leave people in a group
    /// nobody can manage. Unlike leaving, there is no successor to fall back on here - somebody still in
    /// the group asking to stop managing it can promote whoever they want first, or leave.
    /// </summary>
    public void ChangeRole(Guid actorUserId, Guid userId, ChatGroupRole role)
    {
        RequireAdmin(actorUserId);
        var member = FindMember(userId)
            ?? throw new InvalidRequestException("That person isn't in this group.");

        if (member.Role == role)
        {
            return;
        }

        if (member.Role == ChatGroupRole.Admin && AdminCount == 1)
        {
            throw new InvalidRequestException("A group needs at least one admin - promote someone else first.");
        }

        _members.Remove(member);
        _members.Add(member with { Role = role });
    }

    /// <summary>
    /// Puts this group away on one member's own list, or brings it back. Answers false when they are
    /// not in it.
    ///
    /// No admin check, and none of the "a group needs an admin" rules above: archiving changes nothing
    /// about the group, only about one person's view of it. An admin tidying their own list must not
    /// take the group off anybody else's, and a member deciding they are done reading it does not need
    /// permission to stop looking.
    /// </summary>
    public bool SetArchivedFor(Guid userId, bool isArchived)
    {
        if (FindMember(userId) is not { } member)
        {
            return false;
        }

        _members.Remove(member);
        _members.Add(member with { IsArchived = isArchived });
        return true;
    }

    /// <summary>
    /// Refuses unless actorUserId may hand this group's history to recipientUserId. An admin's to give,
    /// for the same reason the membership is: deciding what somebody sees on arrival is the same act as
    /// deciding they arrive at all, and a group where any member could replay the whole conversation to
    /// a newcomer would put that choice in nobody's hands in particular.
    ///
    /// The recipient has to be in the group already - history is shared into a membership, not instead
    /// of one - and nobody shares with themselves, who by definition already has it.
    /// </summary>
    public void EnsureHistoryCanBeSharedWith(Guid actorUserId, Guid recipientUserId)
    {
        RequireAdmin(actorUserId);

        if (actorUserId == recipientUserId)
        {
            throw new InvalidRequestException("You already have this group's history.");
        }

        if (!IsMember(recipientUserId))
        {
            throw new InvalidRequestException("That person isn't in this group.");
        }
    }

    /// <summary>
    /// Whether actorUserId may delete a message sent by senderUserId: their own always, anyone's if they
    /// administer the group. The single place this rule is expressed - see DeleteChatMessageCommandHandler.
    /// </summary>
    public bool CanDeleteMessageFrom(Guid actorUserId, Guid senderUserId)
        => IsMember(actorUserId) && (actorUserId == senderUserId || IsAdmin(actorUserId));

    private int AdminCount => _members.Count(member => member.Role == ChatGroupRole.Admin);

    private void RequireAdmin(Guid actorUserId)
    {
        if (!IsAdmin(actorUserId))
        {
            throw new InvalidRequestException("Only a group admin can do that.");
        }
    }
}
