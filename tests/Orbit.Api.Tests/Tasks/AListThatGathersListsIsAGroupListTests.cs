using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// A list that starts gathering other lists becomes a group list by itself, and stays the reader's to turn
/// off - see TaskList.IsGroup. It was a plain manual toggle until 2026-09-15, then forced on for as long as
/// an entry named a list; the user's list of 2026-09-16 asked for the box to tick itself and still be
/// unticked when somebody wants it off.
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
    /// The save that gives a list its first entry naming another list turns the box on, whatever it said -
    /// which is what "automatically" means for a list that was plain until now.
    /// </summary>
    [Fact]
    public void The_save_that_adds_the_first_naming_entry_turns_it_on()
    {
        var kitchen = TaskList.Create(_userId, "Kitchen", []);
        var renovation = TaskList.Create(_userId, "Renovation", [TaskItem.Create("Skirting", null, false)]);

        renovation.Update(
            "Renovation", [TaskItem.Create("Skirting", null, false), TaskItem.Create("Kitchen", null, false, [kitchen.Id])],
            isGroup: false, isPrivate: false, encryptedContent: null, ItemPriority.Normal);

        Assert.True(renovation.IsGroup);
    }

    /// <summary>
    /// And after that it is the reader's: a save that turns it off is kept while the entry still names a
    /// list. That box used to be locked on, and the user asked for it to be theirs to untick.
    /// </summary>
    [Fact]
    public void A_group_list_can_be_turned_off_while_an_entry_still_names_a_list()
    {
        var kitchen = TaskList.Create(_userId, "Kitchen", []);
        var renovation = AListNamed(kitchen.Id, isGroup: true);

        renovation.Update(
            "Renovation", [TaskItem.Create("Kitchen", null, false, [kitchen.Id])], isGroup: false,
            isPrivate: false, encryptedContent: null, ItemPriority.Normal);

        Assert.False(renovation.IsGroup);
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
    /// A list read back from the database is what it was stored as: a reader who turned the box off must
    /// find it off, so reading a row settles nothing on its own.
    /// </summary>
    [Fact]
    public void A_list_rebuilt_from_a_row_that_says_no_stays_as_it_was_stored()
    {
        var kitchen = TaskList.Create(_userId, "Kitchen", []);
        var now = DateTimeOffset.UtcNow;

        var renovation = TaskList.FromPersistence(
            Guid.NewGuid(), _userId, "Renovation", [TaskItem.Create("Kitchen", null, false, [kitchen.Id])],
            isGroup: false, isPrivate: false, encryptedContent: null, now, now,
            lockedByUserId: null, lockedByUserName: null, lockExpiresAtUtc: null,
            ItemPriority.Normal, isPinned: false);

        Assert.False(renovation.IsGroup);
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
