using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Folders;
using Orbit.Core.Inventories;
using Orbit.Core.Notes;
using Orbit.Core.Notifications;
using Orbit.Core.Places;
using Orbit.Core.Tasks;
using Orbit.Core.Transfer;
using Orbit.Core.Transfer.ExportArchive;
using Orbit.Core.Transfer.ImportArchive;
using Xunit;

namespace Orbit.Api.Tests.Transfer;

/// <summary>
/// Covers what an export promises: everything you own comes out, nothing that isn't yours does, and
/// putting the file back gives you the same things again rather than replacing what is already there.
/// </summary>
public sealed class ArchiveRoundTripTests
{
    [Fact]
    public async Task An_export_carries_every_kind_of_thing_you_own()
    {
        var context = new ArchiveTestContext();
        await context.AddNoteAsync("Shopping list", "Milk");
        await context.AddTaskListAsync("Errands", "Buy milk");
        await context.AddCalendarEventAsync("Dentist");
        await context.AddInventoryAsync("Pantry", "Flour");

        var archive = await context.ExportAsync();

        Assert.Equal(OrbitArchive.CurrentVersion, archive.Version);
        Assert.Single(archive.Notes);
        Assert.Single(archive.TaskLists);
        Assert.Single(archive.CalendarEvents);
        Assert.Single(archive.Inventories);
    }

    [Fact]
    public async Task Someone_elses_things_are_not_yours_to_export()
    {
        var context = new ArchiveTestContext();
        await context.AddNoteAsync("Shopping list", "Milk", ownedBySomeoneElse: true);

        var archive = await context.ExportAsync();

        // A share is access, not ownership - an export that quietly copied their note into a file would
        // be a way of taking it.
        Assert.Empty(archive.Notes);
    }

    [Fact]
    public async Task Importing_gives_you_the_things_back()
    {
        var source = new ArchiveTestContext();
        await source.AddNoteAsync("Shopping list", "Milk", "Bread");
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        var result = await destination.ImportAsync(archive);

        Assert.Equal(1, result.Notes);
        var note = Assert.Single(await destination.OwnNotesAsync());
        Assert.Equal("Shopping list", note.Title);
        Assert.Equal(["Milk", "Bread"], note.Content.Select(line => line.Text));
    }

    [Fact]
    public async Task A_task_lists_items_survive_the_round_trip()
    {
        var source = new ArchiveTestContext();
        await source.AddTaskListAsync("Errands", "Buy milk", "Buy bread");
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        var taskList = Assert.Single(await destination.OwnTaskListsAsync());
        Assert.Equal(["Buy milk", "Buy bread"], taskList.Items.Select(item => item.Description));
    }

    /// <summary>
    /// What an entry is filed under travels with it. An archive is what an account gets back after
    /// losing everything, and coming back unfiled would be coming back changed.
    /// </summary>
    [Fact]
    public async Task What_an_entry_is_filed_under_comes_back_with_it()
    {
        var source = new ArchiveTestContext();
        await source.AddFiledTaskListAsync("Errands", "Buy milk", ["shopping", "weekly"]);
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        var taskList = Assert.Single(await destination.OwnTaskListsAsync());
        Assert.Equal(["shopping", "weekly"], Assert.Single(taskList.Items).Categories);
    }

    /// <summary>
    /// And when it was done: an archive that brought the tick back without its time would put every
    /// finished entry at "not recorded" - or, worse, at the moment of the import.
    /// </summary>
    [Fact]
    public async Task When_an_entry_was_done_comes_back_with_it()
    {
        var source = new ArchiveTestContext();
        var doneAt = new DateTimeOffset(2026, 9, 1, 8, 30, 0, TimeSpan.Zero);
        await source.AddDoneTaskListAsync("Errands", "Buy milk", doneAt);
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        var entry = Assert.Single(Assert.Single(await destination.OwnTaskListsAsync()).Items);
        Assert.True(entry.IsCompleted);
        Assert.Equal(doneAt, entry.CompletedAtUtc);
    }

    /// <summary>
    /// Tags come back on what carried them, and the colours the account gave them come back with them - a
    /// file that restored the words but not their colours would restore every card plain.
    /// </summary>
    [Fact]
    public async Task Tags_and_their_colours_come_back()
    {
        var source = new ArchiveTestContext();
        await source.AddTaggedTaskListAsync("Errands", ["work", "weekly"]);
        await source.ColourTagAsync("work", "#aa3355");
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        Assert.Equal(["work", "weekly"], Assert.Single(await destination.OwnTaskListsAsync()).Tags);
        Assert.Equal(new Orbit.Core.Tags.TagColour("work", "#aa3355"), Assert.Single(await destination.TagColoursAsync()));
    }

    /// <summary>
    /// And an import never overwrites: a tag this account has coloured since the file was written keeps
    /// the colour chosen here.
    /// </summary>
    [Fact]
    public async Task An_imported_colour_does_not_replace_one_chosen_since()
    {
        var source = new ArchiveTestContext();
        await source.ColourTagAsync("work", "#aa3355");
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        await destination.ColourTagAsync("Work", "#113355");
        await destination.ImportAsync(archive);

        Assert.Equal("#113355", Assert.Single(await destination.TagColoursAsync()).Colour);
    }

    [Fact]
    public async Task A_link_between_two_task_lists_is_rebuilt_against_the_new_ones()
    {
        var source = new ArchiveTestContext();
        var targetId = await source.AddTaskListAsync("Groceries", "Buy milk");
        await source.AddLinkedTaskListAsync("Weekend", targetId);
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        // Ids aren't carried, so the link travels as a title and is resolved back to whichever list the
        // import just created - pointing at the old id would point at nothing.
        var imported = await destination.OwnTaskListsAsync();
        var weekend = imported.Single(taskList => taskList.Title == "Weekend");
        var groceries = imported.Single(taskList => taskList.Title == "Groceries");
        Assert.Equal([groceries.Id], Assert.Single(weekend.Items).LinkedTaskListIds);
    }

    [Fact]
    public async Task A_link_to_a_list_that_did_not_come_along_is_dropped()
    {
        var source = new ArchiveTestContext();
        await source.AddLinkedTaskListAsync("Weekend", Guid.NewGuid());
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        var weekend = Assert.Single(await destination.OwnTaskListsAsync());
        // No links at all, rather than a link to nothing: an entry now names a list of them, and
        // "stands for nothing" is an empty list.
        Assert.Empty(Assert.Single(weekend.Items).LinkedTaskListIds);
    }

    /// <summary>
    /// A private list's title is empty on the server, so the link goes by its sealed half - and lands on
    /// that list, not on whichever private list the import happened to make first.
    /// </summary>
    [Fact]
    public async Task A_link_to_a_private_list_comes_back_to_that_list()
    {
        var source = new ArchiveTestContext();
        await source.AddPrivateTaskListAsync("Zm9yZ2V0", "bm9uY2UtYQ==");
        var targetId = await source.AddPrivateTaskListAsync("c2VhbGVk", "bm9uY2UtYg==");
        await source.AddLinkedTaskListAsync("Weekend", targetId);
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        var imported = await destination.OwnTaskListsAsync();
        var target = imported.Single(taskList => taskList.EncryptedContent?.Ciphertext == "c2VhbGVk");
        Assert.Equal([target.Id], Assert.Single(imported.Single(taskList => taskList.Title == "Weekend").Items).LinkedTaskListIds);
    }

    [Fact]
    public async Task A_place_on_a_private_list_comes_back_on_it()
    {
        var source = new ArchiveTestContext();
        await source.AddPrivateTaskListAsync("Zm9yZ2V0", "bm9uY2UtYQ==");
        var targetId = await source.AddPrivateTaskListAsync("c2VhbGVk", "bm9uY2UtYg==");
        await source.AddOpenPlaceAsync("The good bakery", targetId);
        var archive = await source.ExportAsync();

        Assert.Empty(Assert.Single(archive.AllPlaces).TaskListTitles);

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        var target = (await destination.OwnTaskListsAsync()).Single(taskList => taskList.EncryptedContent?.Ciphertext == "c2VhbGVk");
        Assert.Equal([target.Id], Assert.Single(await destination.OwnPlacesAsync()).TaskListIds);
    }

    /// <summary>
    /// A file written before links to private lists travelled by their sealed half wrote the empty title
    /// instead. That names no list, so it is dropped rather than landed on the first private one.
    /// </summary>
    [Fact]
    public async Task An_older_files_empty_title_links_to_no_private_list()
    {
        var archive = new OrbitArchive(
            OrbitArchive.CurrentVersion, DateTimeOffset.UtcNow, [],
            [
                new ArchivedTaskList("", [], false, IsPrivate: true, new ArchivedEncryptedContent("c2VhbGVk", "bm9uY2U="), "Normal"),
                new ArchivedTaskList(
                    "Weekend",
                    [new ArchivedTaskItem("Follows another list", null, false, "", "None", false, "None", new TimeOnly(9, 0), [""])],
                    false, IsPrivate: false, EncryptedContent: null, "Normal")
            ],
            [], []);

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        var weekend = (await destination.OwnTaskListsAsync()).Single(taskList => taskList.Title == "Weekend");
        Assert.Empty(Assert.Single(weekend.Items).LinkedTaskListIds);
    }

    [Fact]
    public async Task A_private_note_travels_sealed()
    {
        var source = new ArchiveTestContext();
        await source.AddPrivateNoteAsync();
        var archive = await source.ExportAsync();

        var archived = Assert.Single(archive.Notes);
        Assert.True(archived.IsPrivate);
        Assert.Equal(string.Empty, archived.Title);
        Assert.Equal("c2VhbGVk", archived.EncryptedContent!.Ciphertext);
    }

    [Fact]
    public async Task An_imported_private_note_is_still_private()
    {
        var source = new ArchiveTestContext();
        await source.AddPrivateNoteAsync();
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        var note = Assert.Single(await destination.OwnNotesAsync());
        Assert.True(note.IsPrivate);
        Assert.Equal("c2VhbGVk", note.EncryptedContent!.Ciphertext);
    }

    [Fact]
    public async Task A_inventories_items_come_along_with_it()
    {
        var source = new ArchiveTestContext();
        await source.AddInventoryAsync("Pantry", "Flour", "Sugar");
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        var inventory = Assert.Single(await destination.OwnInventoriesAsync());
        var items = await destination.ItemsInAsync(inventory.Id);
        Assert.Equal(["Flour", "Sugar"], items.Select(item => item.Name));
    }

    [Fact]
    public async Task Importing_adds_rather_than_replaces()
    {
        var context = new ArchiveTestContext();
        await context.AddNoteAsync("Shopping list", "Milk");
        var archive = await context.ExportAsync();

        await context.ImportAsync(archive);

        // Two copies is a mess someone can fix; an import that overwrote the wrong thing is not.
        Assert.Equal(2, (await context.OwnNotesAsync()).Count);
    }

    [Fact]
    public async Task An_open_place_comes_back_with_its_list()
    {
        var source = new ArchiveTestContext();
        var shoppingId = await source.AddTaskListAsync("Shopping", "Bread");
        await source.AddOpenPlaceAsync("The good bakery", shoppingId);
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        var result = await destination.ImportAsync(archive);

        Assert.Equal(1, result.Places);
        var place = Assert.Single(await destination.OwnPlacesAsync());
        Assert.Equal("The good bakery", place.Name);
        Assert.Equal("Piękna 1, Warszawa", place.Where.Address);
        Assert.Equal(52.2297, place.Where.Latitude);
        Assert.False(place.IsPrivate);
        // Ids aren't carried, so the list is found again by title among the ones the import just made.
        var shopping = Assert.Single(await destination.OwnTaskListsAsync());
        Assert.Equal([shopping.Id], place.TaskListIds);
    }

    /// <summary>
    /// The server writes a private place as it holds one - the words empty, the sealed half beside them -
    /// because it has no key to do anything else. Opening it is the browser's part.
    /// </summary>
    [Fact]
    public async Task A_private_place_leaves_the_server_sealed()
    {
        var source = new ArchiveTestContext();
        await source.AddPrivatePlaceAsync();

        var archived = Assert.Single((await source.ExportAsync()).AllPlaces);

        Assert.True(archived.IsPrivate);
        Assert.Equal(string.Empty, archived.Name);
        Assert.Equal(0, archived.Where.Latitude);
        Assert.Equal("c2VhbGVk", archived.EncryptedContent!.Ciphertext);
    }

    /// <summary>
    /// A file whose private place was opened by the browser still comes back sealed, the way a private
    /// note does - and the opened words do not land in a readable column on the way.
    /// </summary>
    [Fact]
    public async Task An_imported_private_place_is_still_sealed_and_keeps_none_of_its_words()
    {
        var archive = ArchiveOf(new ArchivedPlace(
            "The spare key", "Under the third pot", new ArchivedEventLocation("Piękna 1, Warszawa", 52.2297, 21.0122),
            "", "Normal", [], IsPrivate: true, new ArchivedEncryptedContent("c2VhbGVk", "bm9uY2U=")));

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        var place = Assert.Single(await destination.OwnPlacesAsync());
        Assert.True(place.IsPrivate);
        Assert.Equal("c2VhbGVk", place.EncryptedContent!.Ciphertext);
        Assert.Equal(string.Empty, place.Name);
        Assert.Equal(0, place.Where.Latitude);
    }

    /// <summary>
    /// Private with nothing sealed is the one pairing the server cannot store: it has no key to seal the
    /// words with, and keeping them readable would publish what the file says is private.
    /// </summary>
    [Fact]
    public async Task A_private_place_with_nothing_sealed_is_left_out_and_not_counted()
    {
        var archive = ArchiveOf(new ArchivedPlace(
            "The spare key", "", new ArchivedEventLocation("Piękna 1", 52.2297, 21.0122),
            "", "Normal", [], IsPrivate: true, EncryptedContent: null));

        var destination = new ArchiveTestContext();
        var result = await destination.ImportAsync(archive);

        Assert.Equal(0, result.Places);
        Assert.Empty(await destination.OwnPlacesAsync());
    }

    [Fact]
    public async Task Someone_elses_place_is_not_yours_to_export()
    {
        var context = new ArchiveTestContext();
        await context.AddOpenPlaceAsync("Their bakery", ownedBySomeoneElse: true);

        Assert.Empty((await context.ExportAsync()).AllPlaces);
    }

    /// <summary>A file written before places were exported says nothing about them, and still imports.</summary>
    [Fact]
    public async Task A_file_written_before_places_existed_still_imports()
    {
        const string json = """
            {"version":1,"exportedAtUtc":"2026-09-01T00:00:00+00:00","notes":[],"taskLists":[],"calendarEvents":[],"inventories":[]}
            """;
        var archive = System.Text.Json.JsonSerializer.Deserialize<OrbitArchive>(
            json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var result = await new ArchiveTestContext().ImportAsync(archive);

        Assert.Empty(archive.AllPlaces);
        Assert.Equal(0, result.Places);
    }

    [Fact]
    public void Closing_an_archive_empties_only_the_private_places()
    {
        var archive = ArchiveOf(
            new ArchivedPlace(
                "The spare key", "Under the pot", new ArchivedEventLocation("Piękna 1", 52.2297, 21.0122),
                "", "Normal", [], IsPrivate: true, new ArchivedEncryptedContent("c2VhbGVk", "bm9uY2U=")),
            new ArchivedPlace(
                "The good bakery", "", new ArchivedEventLocation("Rynek 2", 50.06, 19.94),
                "", "Normal", [], IsPrivate: false, EncryptedContent: null));

        var closed = archive.WithPrivatePlacesClosed().AllPlaces;

        Assert.Equal(string.Empty, closed[0].Name);
        Assert.Equal(string.Empty, closed[0].Description);
        Assert.Equal(0, closed[0].Where.Latitude);
        Assert.Equal("c2VhbGVk", closed[0].EncryptedContent!.Ciphertext);
        Assert.Equal("The good bakery", closed[1].Name);
    }

    private static OrbitArchive ArchiveOf(params ArchivedPlace[] places)
        => new(OrbitArchive.CurrentVersion, DateTimeOffset.UtcNow, [], [], [], [], places);

    [Fact]
    public async Task An_archive_from_a_version_this_does_not_know_is_refused()
    {
        var context = new ArchiveTestContext();
        var archive = new OrbitArchive(Version: 99, DateTimeOffset.UtcNow, [], [], [], []);

        // Refused outright rather than read for the parts that happen to look familiar.
        await Assert.ThrowsAsync<InvalidRequestException>(() => context.ImportAsync(archive));
    }

    /// <summary>
    /// An export carries the tabs themselves, not only what is filed in them: a folder somebody made and
    /// has not put anything in yet is still a tab, and an import that invented folders out of what it
    /// found filed would lose it.
    /// </summary>
    [Fact]
    public async Task An_export_carries_the_folders_and_says_what_is_filed_in_them()
    {
        var context = new ArchiveTestContext();
        var work = await context.AddFolderAsync("Work", FolderScope.Notes);
        await context.AddFolderAsync("Empty", FolderScope.Tasks);
        var week = await context.AddFolderAsync("This week", FolderScope.Calendar);
        var kitchen = await context.AddFolderAsync("Kitchen", FolderScope.Inventories);
        await context.AddNoteInAsync("Shopping list", work);
        await context.AddCalendarEventInAsync("Dentist", week);
        await context.AddInventoryInAsync("Pantry", kitchen);
        await context.AddNoteAsync("Filed nowhere", "Milk");

        var archive = await context.ExportAsync();

        Assert.Equal(
            [("Work", "Notes"), ("Empty", "Tasks"), ("This week", "Calendar"), ("Kitchen", "Inventories")],
            archive.AllFolders.Select(folder => (folder.Name, folder.Scope)));
        Assert.Equal("Work", archive.Notes.Single(note => note.Title == "Shopping list").Folder);
        Assert.Null(archive.Notes.Single(note => note.Title == "Filed nowhere").Folder);
        Assert.Equal("This week", Assert.Single(archive.CalendarEvents).Folder);
        Assert.Equal("Kitchen", Assert.Single(archive.Inventories).Folder);
    }

    [Fact]
    public async Task Importing_puts_everything_back_in_the_folder_it_came_from()
    {
        var source = new ArchiveTestContext();
        var work = await source.AddFolderAsync("Work", FolderScope.Notes);
        var week = await source.AddFolderAsync("This week", FolderScope.Calendar);
        var kitchen = await source.AddFolderAsync("Kitchen", FolderScope.Inventories);
        var moving = await source.AddFolderAsync("Moving", FolderScope.Tasks);
        await source.AddNoteInAsync("Shopping list", work);
        await source.AddCalendarEventInAsync("Dentist", week);
        await source.AddInventoryInAsync("Pantry", kitchen);
        await source.AddTaskListInAsync("Errands", moving);
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        var folders = await destination.OwnFoldersAsync();
        var noteFolder = folders.Single(folder => folder is { Name: "Work", Scope: FolderScope.Notes });
        var eventFolder = folders.Single(folder => folder is { Name: "This week", Scope: FolderScope.Calendar });
        var inventoryFolder = folders.Single(folder => folder is { Name: "Kitchen", Scope: FolderScope.Inventories });
        var taskListFolder = folders.Single(folder => folder is { Name: "Moving", Scope: FolderScope.Tasks });
        Assert.Equal(noteFolder.Id, (await destination.OwnNotesAsync()).Single().FolderId);
        Assert.Equal(eventFolder.Id, (await destination.OwnCalendarEventsAsync()).Single().FolderId);
        Assert.Equal(inventoryFolder.Id, (await destination.OwnInventoriesAsync()).Single().FolderId);
        Assert.Equal(taskListFolder.Id, (await destination.OwnTaskListsAsync()).Single().FolderId);
    }

    /// <summary>
    /// A tab the account already has is used rather than made a second time - two tabs called "Work" on
    /// one page is a mess nobody asked for, and nothing about the existing one is changed by it.
    /// </summary>
    [Fact]
    public async Task Importing_files_into_a_folder_this_account_already_has()
    {
        var source = new ArchiveTestContext();
        var work = await source.AddFolderAsync("Work", FolderScope.Notes);
        await source.AddNoteInAsync("Shopping list", work);
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        var theirWork = await destination.AddFolderAsync("Work", FolderScope.Notes);
        await destination.ImportAsync(archive);

        Assert.Equal("Work", Assert.Single(await destination.OwnFoldersAsync()).Name);
        Assert.Equal(theirWork, (await destination.OwnNotesAsync()).Single().FolderId);
    }

    /// <summary>
    /// The same name on two pages is two folders, so a note's "Work" never resolves to the task lists'.
    /// </summary>
    [Fact]
    public async Task A_folder_name_is_only_read_on_its_own_page()
    {
        var context = new ArchiveTestContext();
        var theirs = await context.AddFolderAsync("Work", FolderScope.Tasks);
        var archive = new OrbitArchive(
            OrbitArchive.CurrentVersion, DateTimeOffset.UtcNow,
            [new ArchivedNote("Shopping list", [], IsPrivate: false, EncryptedContent: null, Tags: null, Folder: "Work")],
            [], [], [], Folders: [new ArchivedFolder("Work", nameof(FolderScope.Notes))]);

        await context.ImportAsync(archive);

        var folders = await context.OwnFoldersAsync();
        Assert.Equal(2, folders.Count);
        Assert.NotEqual(theirs, (await context.OwnNotesAsync()).Single().FolderId);
        Assert.Equal(
            folders.Single(folder => folder.Scope == FolderScope.Notes).Id,
            (await context.OwnNotesAsync()).Single().FolderId);
    }

    /// <summary>
    /// A folder the file does not carry leaves what named it unfiled, rather than filed at random - the
    /// rule a link to a list that did not come along follows.
    /// </summary>
    [Fact]
    public async Task Something_naming_a_folder_the_file_does_not_carry_comes_back_unfiled()
    {
        var context = new ArchiveTestContext();
        var archive = new OrbitArchive(
            OrbitArchive.CurrentVersion, DateTimeOffset.UtcNow,
            [new ArchivedNote("Shopping list", [], IsPrivate: false, EncryptedContent: null, Tags: null, Folder: "Gone")],
            [], [], []);

        await context.ImportAsync(archive);

        Assert.Empty(await context.OwnFoldersAsync());
        Assert.Null((await context.OwnNotesAsync()).Single().FolderId);
    }

    /// <summary>What these lists say when they are late, and when they say it daily - the same for all of them.</summary>
    private static readonly TaskItemReminders NineOClock =
        new(NotificationChannel.None, Daily: false, NotificationChannel.None, new TimeOnly(9, 0));

    /// <summary>
    /// Putting something away is a decision its owner took, so a file that dropped it would hand back an
    /// account whose Archived tab had been emptied onto its pages - the opposite of what was asked for.
    /// </summary>
    [Fact]
    public async Task Importing_leaves_what_was_put_away_put_away()
    {
        var source = new ArchiveTestContext();
        await source.AddPutAwayAsync("Old receipts");
        await source.AddNoteAsync("Still on the page", "Milk");
        var archive = await source.ExportAsync();

        Assert.True(archive.Notes.Single(note => note.Title == "Old receipts").IsArchived);
        Assert.False(archive.Notes.Single(note => note.Title == "Still on the page").IsArchived);
        Assert.True(Assert.Single(archive.TaskLists).IsArchived);
        Assert.True(Assert.Single(archive.CalendarEvents).IsArchived);
        Assert.True(Assert.Single(archive.Inventories).IsArchived);

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        Assert.True((await destination.OwnNotesAsync()).Single(note => note.Title == "Old receipts").IsArchived);
        Assert.False((await destination.OwnNotesAsync()).Single(note => note.Title == "Still on the page").IsArchived);
        Assert.True((await destination.OwnTaskListsAsync()).Single().IsArchived);
        Assert.True((await destination.OwnCalendarEventsAsync()).Single().IsArchived);
        Assert.True((await destination.OwnInventoriesAsync()).Single().IsArchived);
    }

    /// <summary>
    /// A file written before things could be put away says nothing about it, and that reads as an account
    /// that had put nothing away - not as a refusal, and not as everything archived.
    /// </summary>
    [Fact]
    public async Task A_file_that_says_nothing_about_archiving_imports_nothing_archived()
    {
        var context = new ArchiveTestContext();
        var archive = new OrbitArchive(
            OrbitArchive.CurrentVersion, DateTimeOffset.UtcNow,
            [new ArchivedNote("Shopping list", [], IsPrivate: false, EncryptedContent: null)],
            [], [], []);

        await context.ImportAsync(archive);

        Assert.False((await context.OwnNotesAsync()).Single().IsArchived);
    }

    /// <summary>
    /// A rule across a note travels with its stamp as the words it was made with - see
    /// NoteSeparatorLine.Stamp. Worked out again on import it would say the day the file was opened,
    /// which is the one thing putting a date in a note must not do.
    /// </summary>
    [Fact]
    public async Task A_rule_across_a_note_comes_back_saying_what_it_said()
    {
        const string stamp = "Tuesday, 15 September 2026 11:20";
        var source = new ArchiveTestContext();
        await source.AddNoteWithARuleAsync("Kitchen", stamp);
        var archive = await source.ExportAsync();

        var written = Assert.Single(archive.Notes).Content;
        Assert.Equal(stamp, written[1].Separator!.Stamp);
        Assert.Equal(string.Empty, written[1].Text);

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        var content = (await destination.OwnNotesAsync()).Single().Content;
        Assert.True(content[1].IsASeparator);
        Assert.Equal(stamp, content[1].Separator!.Stamp);
        Assert.Equal(["Bought the paint", "Still to hang the door"], new[] { content[0].Text, content[2].Text });
    }

    /// <summary>
    /// A file written before rules existed says nothing about them, and that reads as a note with none -
    /// not as a refusal, and not as a line that is somehow both words and a rule.
    /// </summary>
    [Fact]
    public async Task A_file_that_says_nothing_about_rules_imports_none()
    {
        var context = new ArchiveTestContext();
        var archive = new OrbitArchive(
            OrbitArchive.CurrentVersion, DateTimeOffset.UtcNow,
            [new ArchivedNote("Shopping", [new ArchivedNoteLine("Milk", false, false)], IsPrivate: false, EncryptedContent: null)],
            [], [], []);

        await context.ImportAsync(archive);

        Assert.False((await context.OwnNotesAsync()).Single().Content.Single().IsASeparator);
    }

    private sealed class ArchiveTestContext
    {
        private readonly InMemoryNoteRepository _noteRepository = new();
        private readonly InMemoryTaskRepository _taskRepository = new();
        private readonly InMemoryCalendarEventRepository _calendarEventRepository = new();
        private readonly InMemoryInventoryRepository _inventoryRepository = new();
        private readonly InMemoryInventoryItemRepository _inventoryItemRepository = new();
        private readonly InMemoryPlaceRepository _placeRepository = new();
        private readonly InMemoryTagColourRepository _tagColourRepository = new();
        private readonly InMemoryFolderRepository _folderRepository = new();

        private Guid UserId { get; } = Guid.NewGuid();

        public async Task AddOpenPlaceAsync(string name, Guid? taskListId = null, bool ownedBySomeoneElse = false)
            => await _placeRepository.AddAsync(
                Place.Create(
                    ownedBySomeoneElse ? Guid.NewGuid() : UserId, name, "Sourdough",
                    new EventLocation("Piękna 1, Warszawa", 52.2297, 21.0122),
                    taskListIds: taskListId is { } id ? [id] : null, isPrivate: false),
                CancellationToken.None);

        public async Task AddPrivatePlaceAsync()
            => await _placeRepository.AddAsync(
                Place.Create(
                    UserId, "The spare key", "Under the third pot", new EventLocation("Piękna 1, Warszawa", 52.2297, 21.0122),
                    encryptedContent: new EncryptedPayload("c2VhbGVk", "bm9uY2U=")),
                CancellationToken.None);

        public Task<IReadOnlyList<Place>> OwnPlacesAsync() => _placeRepository.GetAllAsync(UserId, updatedSinceUtc: null, CancellationToken.None);

        public async Task AddNoteAsync(string title, params string[] lines)
            => await AddNoteAsync(title, ownedBySomeoneElse: false, lines);

        public async Task AddNoteAsync(string title, string line, bool ownedBySomeoneElse)
            => await AddNoteAsync(title, ownedBySomeoneElse, line);

        private async Task AddNoteAsync(string title, bool ownedBySomeoneElse, params string[] lines)
        {
            var note = Note.Create(
                ownedBySomeoneElse ? Guid.NewGuid() : UserId, title, lines.Select(NoteContentLine.PlainText).ToList());
            await _noteRepository.AddAsync(note, CancellationToken.None);
        }


        /// <summary>A note with a dated rule across it, which the file has to carry back out unchanged.</summary>
        public async Task AddNoteWithARuleAsync(string title, string stamp)
            => await _noteRepository.AddAsync(
                Note.Create(UserId, title, [
                    NoteContentLine.PlainText("Bought the paint"),
                    NoteContentLine.OfSeparator(stamp),
                    NoteContentLine.PlainText("Still to hang the door")
                ]),
                CancellationToken.None);

        public async Task AddPrivateNoteAsync()
            => await _noteRepository.AddAsync(
                Note.Create(UserId, string.Empty, [], isPrivate: true, new EncryptedPayload("c2VhbGVk", "bm9uY2U=")),
                CancellationToken.None);

        public async Task<Guid> AddTaskListAsync(string title, params string[] descriptions)
        {
            var taskList = TaskList.Create(UserId, title, descriptions.Select(description => Item(description)).ToList());
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList.Id;
        }

        /// <summary>An empty list tagged with <paramref name="tags"/>.</summary>
        public async Task AddTaggedTaskListAsync(string title, IReadOnlyList<string> tags)
            => await _taskRepository.AddAsync(TaskList.Create(UserId, title, [], tags: tags), CancellationToken.None);

        public Task ColourTagAsync(string tag, string colour)
            => _tagColourRepository.SetAsync(UserId, tag, colour, CancellationToken.None);

        public Task<IReadOnlyList<Orbit.Core.Tags.TagColour>> TagColoursAsync()
            => _tagColourRepository.GetAllAsync(UserId, CancellationToken.None);

        /// <summary>A list of one entry that is done, and was done at <paramref name="doneAtUtc"/>.</summary>
        public async Task<Guid> AddDoneTaskListAsync(string title, string description, DateTimeOffset doneAtUtc)
        {
            var taskList = TaskList.Create(
                UserId, title,
                [TaskItem.FromPersistence(
                    Guid.NewGuid(), description, null, true, null, TaskItemReminders.Default, completedAtUtc: doneAtUtc)]);
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList.Id;
        }

        public async Task<Guid> AddPrivateTaskListAsync(string ciphertext, string nonce)
        {
            var taskList = TaskList.Create(
                UserId, string.Empty, [], isPrivate: true, encryptedContent: new EncryptedPayload(ciphertext, nonce));
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList.Id;
        }

        public async Task AddLinkedTaskListAsync(string title, Guid linkedTaskListId)
        {
            var taskList = TaskList.Create(
                UserId, title,
                [TaskItem.Create(
                    "Follows another list", null, false, [linkedTaskListId], NineOClock)]);
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
        }

        public async Task AddCalendarEventAsync(string title)
        {
            var details = new CalendarEventDetails(
                title, "Bring the paperwork", null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
                false, null, [], [15], NotificationChannel.None);
            await _calendarEventRepository.AddAsync(CalendarEvent.Create(UserId, details), CancellationToken.None);
        }

        public async Task AddInventoryAsync(string name, params string[] itemNames)
        {
            var inventory = Inventory.Create(UserId, name);
            await _inventoryRepository.AddAsync(inventory, CancellationToken.None);
            foreach (var itemName in itemNames)
            {
                await _inventoryItemRepository.AddAsync(
                    InventoryItem.Create(inventory.Id, itemName, "Food", ["Dry goods"], 1, null, InventoryUnit.Piece, null, NotificationChannel.None),
                    CancellationToken.None);
            }
        }

        public Task<OrbitArchive> ExportAsync()
            => new ExportArchiveQueryHandler(
                    _noteRepository, _taskRepository, _calendarEventRepository, _inventoryRepository, _inventoryItemRepository,
                    _placeRepository, _tagColourRepository, _folderRepository)
                .HandleAsync(new ExportArchiveQuery(UserId), CancellationToken.None);

        public Task<ImportArchiveResult> ImportAsync(OrbitArchive archive)
            => new ImportArchiveCommandHandler(
                    _noteRepository, _taskRepository, _calendarEventRepository, _inventoryRepository, _inventoryItemRepository,
                    _placeRepository, _tagColourRepository, _folderRepository)
                .HandleAsync(new ImportArchiveCommand(UserId, archive), CancellationToken.None);

        /// <summary>A tab of this account's own, on <paramref name="scope"/>'s page.</summary>
        public async Task<Guid> AddFolderAsync(string name, FolderScope scope)
        {
            var folder = Folder.Create(UserId, name, scope);
            await _folderRepository.AddAsync(folder, CancellationToken.None);
            return folder.Id;
        }

        public Task<IReadOnlyList<Folder>> OwnFoldersAsync() => _folderRepository.GetAllAsync(UserId, CancellationToken.None);

        public async Task AddNoteInAsync(string title, Guid folderId)
            => await _noteRepository.AddAsync(
                Note.Create(UserId, title, [NoteContentLine.PlainText("Milk")], folderId: folderId), CancellationToken.None);

        public async Task AddTaskListInAsync(string title, Guid folderId)
            => await _taskRepository.AddAsync(
                TaskList.Create(UserId, title, [], folderId: folderId), CancellationToken.None);

        public async Task AddCalendarEventInAsync(string title, Guid folderId)
        {
            var details = new CalendarEventDetails(
                title, "Bring the paperwork", null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
                false, null, [], [15], NotificationChannel.None);
            await _calendarEventRepository.AddAsync(CalendarEvent.Create(UserId, details, folderId), CancellationToken.None);
        }

        public async Task AddInventoryInAsync(string name, Guid folderId)
            => await _inventoryRepository.AddAsync(
                Inventory.Create(UserId, name, folderId: folderId), CancellationToken.None);

        /// <summary>One of each kind, put away - the state the file has to carry back out again.</summary>
        public async Task AddPutAwayAsync(string name)
        {
            var note = Note.Create(UserId, name, [NoteContentLine.PlainText("Milk")]);
            note.Archive(true);
            await _noteRepository.AddAsync(note, CancellationToken.None);

            var taskList = TaskList.Create(UserId, name, []);
            taskList.Archive(true);
            await _taskRepository.AddAsync(taskList, CancellationToken.None);

            var details = new CalendarEventDetails(
                name, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
                false, null, [], [], NotificationChannel.None);
            var calendarEvent = CalendarEvent.Create(UserId, details, folderId: null);
            calendarEvent.Archive(true);
            await _calendarEventRepository.AddAsync(calendarEvent, CancellationToken.None);

            var inventory = Inventory.Create(UserId, name);
            inventory.Archive(true);
            await _inventoryRepository.AddAsync(inventory, CancellationToken.None);
        }

        public Task<IReadOnlyList<Note>> OwnNotesAsync() => _noteRepository.GetAllAsync(UserId, updatedSinceUtc: null, CancellationToken.None);

        public Task<IReadOnlyList<TaskList>> OwnTaskListsAsync() => _taskRepository.GetAllAsync(UserId, updatedSinceUtc: null, CancellationToken.None);

        public Task<IReadOnlyList<Inventory>> OwnInventoriesAsync() => _inventoryRepository.GetAllAsync(UserId, updatedSinceUtc: null, CancellationToken.None);

        public Task<IReadOnlyList<CalendarEvent>> OwnCalendarEventsAsync()
            => _calendarEventRepository.GetAllAsync(UserId, updatedSinceUtc: null, CancellationToken.None);

        public Task<IReadOnlyList<InventoryItem>> ItemsInAsync(Guid inventoryId)
            => _inventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None);

        public async Task AddFiledTaskListAsync(string title, string description, IReadOnlyList<string> categories)
        {
            var taskList = TaskList.Create(UserId, title, [Item(description, categories)]);
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
        }

        private static TaskItem Item(string description, IReadOnlyList<string>? categories = null)
            => TaskItem.Create(
                description, null, false, null, NineOClock, categories: categories);
    }
}
