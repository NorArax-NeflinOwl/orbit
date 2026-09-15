using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// A list whose entries point at other lists <b>is</b> a group list, whatever the box says - see
/// TaskList.IsGroup. It was a plain manual toggle until 2026-09-15, so a list somebody built by adding
/// entries that name lists gathered nothing until they happened to notice a box; and a save from a
/// client that has never drawn that box turned it back off.
///
/// The rule lives in the domain rather than in an editor's form, which is the user's decision of that
/// date: a rule that only holds where somebody is looking is not a rule.
/// </summary>
public sealed class AListThatGathersListsIsAGroupListTests
{
    private readonly Guid _userId = Guid.NewGuid();

    private TaskList AListNamed(Guid other, bool isGroup = false)
        => TaskList.Create(_userId, "Renovation", [TaskItem.Create("Kitchen", null, false, [other])], isGroup);

    [Fact]
    public void A_list_with_an_entry_naming_another_list_is_a_group_list_without_being_asked()
    {
        var kitchen = TaskList.Create(_userId, "Kitchen", []);

        var renovation = AListNamed(kitchen.Id);

        Assert.True(renovation.IsGroup);
    }

    [Fact]
    public void A_list_whose_entries_name_nothing_is_whatever_it_was_asked_to_be()
    {
        var plain = TaskList.Create(_userId, "Errands", [TaskItem.Create("Milk", null, false)]);

        Assert.False(plain.IsGroup);
        Assert.False(plain.GathersOtherLists);
    }

    /// <summary>
    /// The case the rule exists for: a save that says "no" while an entry says otherwise. That is what
    /// every client written before this sends, and what a browser tab opened yesterday still sends.
    /// </summary>
    [Fact]
    public void A_save_that_says_it_is_not_a_group_does_not_turn_it_off_while_an_entry_names_a_list()
    {
        var kitchen = TaskList.Create(_userId, "Kitchen", []);
        var renovation = AListNamed(kitchen.Id, isGroup: true);

        renovation.Update(
            "Renovation", [TaskItem.Create("Kitchen", null, false, [kitchen.Id])], isGroup: false,
            isPrivate: false, encryptedContent: null, ItemPriority.Normal);

        Assert.True(renovation.IsGroup);
    }

    /// <summary>
    /// And taking the last such entry off gives the answer back: nothing forces it then, so what the
    /// save says is what the list is, and the reader may turn it off.
    /// </summary>
    [Fact]
    public void Taking_the_last_naming_entry_off_lets_it_be_turned_off_again()
    {
        var kitchen = TaskList.Create(_userId, "Kitchen", []);
        var renovation = AListNamed(kitchen.Id);

        renovation.Update(
            "Renovation", [TaskItem.Create("Skirting", null, false)], isGroup: false,
            isPrivate: false, encryptedContent: null, ItemPriority.Normal);

        Assert.False(renovation.IsGroup);
        Assert.False(renovation.GathersOtherLists);
    }

    /// <summary>
    /// A list read back from the database answers the same way, so a row stored before the migration
    /// that brought them into line is still drawn as what it is.
    /// </summary>
    [Fact]
    public void A_list_rebuilt_from_a_row_that_says_no_is_still_a_group_list()
    {
        var kitchen = TaskList.Create(_userId, "Kitchen", []);
        var now = DateTimeOffset.UtcNow;

        var renovation = TaskList.FromPersistence(
            Guid.NewGuid(), _userId, "Renovation", [TaskItem.Create("Kitchen", null, false, [kitchen.Id])],
            isGroup: false, isPrivate: false, encryptedContent: null, now, now,
            lockedByUserId: null, lockedByUserName: null, lockExpiresAtUtc: null,
            ItemPriority.Normal, isPinned: false);

        Assert.True(renovation.IsGroup);
    }

    /// <summary>
    /// A sealed list keeps no readable entries (see TaskList.ReadableOrSealed), so there is nothing here
    /// to read the answer off - and a private list must not be made a group by entries this side cannot
    /// see. It keeps what it was given.
    /// </summary>
    [Fact]
    public void A_sealed_list_is_left_with_the_answer_it_was_given()
    {
        var sealedList = TaskList.Create(
            _userId, string.Empty, [], isGroup: false, isPrivate: true,
            encryptedContent: new EncryptedPayload("c2VhbGVk", "bm9uY2U="));

        Assert.False(sealedList.IsGroup);
        Assert.False(sealedList.GathersOtherLists);
    }
}
