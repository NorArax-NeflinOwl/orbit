using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Chat;
using Orbit.Contracts.Inventories;
using Orbit.Contracts.Notifications;
using Orbit.Contracts.Tasks;
using Orbit.Core.Tasks;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using System.Text.Json;
using Orbit.Contracts.Notes;
using Orbit.Web.Components;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// What an entry's form offers, which now depends on what the entry is. The row itself reports - what it
/// says, when it is due, what kind it is - and everything editable waits behind the toggle, because a
/// list of thirty items was thirty rows of boxes. The list's own title and description are here too:
/// they sit at the top of the same form.
/// </summary>
public sealed class TaskEditorItemFormTests : OrbitTestContext
{
    private static readonly Guid TaskListId = Guid.NewGuid();

    /// <summary>One of the other lists this account has, with an id a test can name.</summary>
    private static readonly Guid OtherTaskListId = Guid.NewGuid();

    /// <summary>The storage this list is measured against, for a test that wants one. Null for most.</summary>
    private InventoryDto? _linkedInventory;
    private static readonly Guid ItemId = Guid.NewGuid();

    public TaskEditorItemFormTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterAuthentication();
        RegisterPermissions();
    }

    [Fact]
    public void The_row_itself_carries_nothing_to_type_into()
    {
        RegisterApiClients(AnItem());

        var cut = Render();

        // The date, the time and Remove all moved behind the toggle. What is left is what somebody
        // reads down a list for.
        var row = cut.Find(".editor-item-summary");
        Assert.Empty(row.QuerySelectorAll("input[type=date]"));
        Assert.Empty(row.QuerySelectorAll("input[type=time]"));
        Assert.DoesNotContain("Remove", row.TextContent);
    }

    [Fact]
    public void A_dated_entry_still_says_so_on_the_row()
    {
        // Hiding the boxes must not hide the fact. Opening every item to find the one with a date on it
        // would be a worse list than the one full of boxes.
        RegisterApiClients(AnItem(dueDateUtc: new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero)));

        var cut = Render();

        Assert.Contains("14.09.2026", cut.Find(".editor-item-summary").TextContent);
    }

    [Fact]
    public void Opening_a_checklist_entry_offers_the_fields_a_day_of_work_needs()
    {
        RegisterApiClients(AnItem());
        var cut = Render();

        ExpandTheOnlyItem(cut);

        var details = cut.Find(".editor-item-details").TextContent;
        Assert.Contains("Type", details);
        Assert.Contains("Due date", details);
        Assert.Contains("Due time", details);
        Assert.Contains("Remove", details);
    }

    /// <summary>
    /// An entry's own address - "/tasks/{listId}/items/{itemId}/edit" - lands on the list's form with
    /// that one entry already unfolded, the same as the toggle does by hand. See TaskItemSummary's and
    /// TaskListChecklist's GoTo*, the two places that link here now that this is a route rather than a
    /// query string nobody else could point at.
    /// </summary>
    [Fact]
    public void The_entrys_own_address_opens_it_already_unfolded()
    {
        RegisterApiClients(AnItem());

        var cut = RenderComponent<TaskEditor>(parameters => parameters
            .Add(editor => editor.Id, TaskListId)
            .Add(editor => editor.ItemToOpen, ItemId));

        var details = cut.Find(".editor-item-details").TextContent;
        Assert.Contains("Due date", details);
    }

    /// <summary>
    /// An appointment already says when it is, in the event's own start and end. Asking it for a due
    /// date as well left two answers to one question, and nothing saying which the calendar reads.
    /// </summary>
    [Theory]
    [InlineData(nameof(TaskItemKind.Calendar))]
    [InlineData(nameof(TaskItemKind.Inventory))]
    public void Only_a_checklist_entry_is_asked_when_it_is_due(string kind)
    {
        RegisterApiClients(AnItem(kind: kind));
        var cut = Render();

        ExpandTheOnlyItem(cut);

        var details = cut.Find(".editor-item-details").TextContent;
        Assert.DoesNotContain("Due date", details);
        Assert.DoesNotContain("Due time", details);
    }

    /// <summary>And it is not reported on the row either - see Tasks.razor, which follows the same rule.</summary>
    [Fact]
    public void A_dated_appointment_does_not_report_its_due_date_on_the_row()
    {
        RegisterApiClients(AnItem(
            kind: nameof(TaskItemKind.Calendar),
            dueDateUtc: new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero)));

        var cut = Render();

        Assert.DoesNotContain("14.09.2026", cut.Find(".editor-item-summary").TextContent);
    }

    /// <summary>
    /// An entry of this kind describes something the work needs, on a list that has no storage behind it
    /// yet - and the description is kept on the entry until "Generate inventory" turns it into a shelf
    /// row (see Orbit.Core.Tasks.TaskItemProduct). It used to name the thing and nothing else, so
    /// everything about it had to be typed again on the storage afterwards.
    ///
    /// What it still does not do is point at an existing product: the shelf comes from the list, not the
    /// other way round - see GenerateInventoryFromTaskListCommandHandler.
    /// </summary>
    [Fact]
    public void An_inventory_entry_describes_the_thing_it_names()
    {
        RegisterApiClients(AnItem(kind: nameof(TaskItemKind.Inventory)));
        var cut = Render();

        ExpandTheOnlyItem(cut);

        var detailsElement = cut.Find(".editor-item-details");
        var details = detailsElement.TextContent;
        Assert.Contains("Amount", details);
        Assert.Contains("Product type", details);
        Assert.Contains("Check every round", details);
        // The entry's own words are the product's name, so the form does not ask for one - and nothing
        // here offers a product already on some shelf to point at instead.
        Assert.DoesNotContain("Item name", details);
        Assert.Contains("Goes on the shelf", details);
    }

    /// <summary>
    /// A calendar entry can invite people whether the list it is on has been saved yet or not. The
    /// contacts used to be read only alongside an existing list, so a Calendar entry on a brand new one
    /// said there was nobody to invite - which is not the same thing as having no contacts.
    /// </summary>
    [Fact]
    public void A_calendar_entry_on_a_new_list_can_still_invite_somebody()
    {
        RegisterApiClients(AnItem());
        // No id: a list being made rather than one being edited, which is where the contacts went
        // unread.
        var cut = RenderComponent<TaskEditor>();

        ClickButtonSaying(cut, "Add item");
        if (cut.FindAll(".editor-item-details").Count == 0)
        {
            cut.Find(".editor-item-toggle").Click();
        }

        cut.FindAll("select").First(box => box.QuerySelectorAll("option").Any(
            option => option.TextContent.Trim() == "Calendar")).Change(nameof(TaskItemKind.Calendar));

        Assert.NotEmpty(cut.FindAll("#guestContactSelect"));
    }

    /// <summary>
    /// Offered for every saved entry, saying so when there is nowhere to move it. Left out entirely, it
    /// read as a setting that had gone missing rather than as an answer.
    /// </summary>
    [Fact]
    public void A_saved_entry_is_always_offered_somewhere_to_move_to()
    {
        RegisterApiClients(AnItem());
        var cut = Render();

        ExpandTheOnlyItem(cut);

        Assert.Contains("Move to list", cut.Find(".editor-item-details").TextContent);
    }

    [Fact]
    public void A_checklist_entry_keeps_the_fields_a_checklist_entry_had()
    {
        RegisterApiClients(AnItem());
        var cut = Render();

        ExpandTheOnlyItem(cut);

        var details = cut.Find(".editor-item-details").TextContent;
        Assert.Contains("Stands for these lists", details);
        Assert.Contains("Overdue notification", details);
        Assert.Contains("Remind daily", details);
    }

    [Fact]
    public void An_inventory_entry_is_asked_nothing_a_checklist_entry_is_asked()
    {
        // Its fields are the shelf item's - see TaskEditor's Inventory branch. Offering "Link to list"
        // beside them would be offering something that means nothing for this kind.
        RegisterApiClients(AnItem(kind: nameof(TaskItemKind.Inventory)));
        var cut = Render();

        ExpandTheOnlyItem(cut);

        Assert.DoesNotContain("Link to list", cut.Find(".editor-item-details").TextContent);
    }

    [Fact]
    public void A_daily_reminder_with_no_hour_is_refused_rather_than_sent_at_midnight()
    {
        RegisterApiClients(AnItem(remindDaily: true));
        var cut = Render();

        ClickButtonSaying(cut, "Save");

        // An hour nobody chose is worse than being asked for one.
        Assert.Contains("needs a time", cut.Markup);
        Assert.Null(_lastSavedJson);
    }

    [Fact]
    public void A_daily_reminder_with_an_hour_saves()
    {
        RegisterApiClients(AnItem(remindDaily: true, dailyReminderTimeOfDay: new TimeOnly(7, 30)));
        var cut = Render();

        ClickButtonSaying(cut, "Save");

        Assert.NotNull(_lastSavedJson);
    }

    /// <summary>
    /// A word at a time, with "+" between them - see TagField. What is in the box when Save is pressed
    /// counts whether or not "+" was pressed on it, which is the whole point of the control: the button
    /// is for adding a second word, not for confirming the first.
    /// </summary>
    [Fact]
    public void What_an_entry_is_about_is_added_a_word_at_a_time()
    {
        RegisterApiClients(AnItem());
        var cut = Render();
        ExpandTheOnlyItem(cut);

        cut.Find(".tag-field-input").Input("shopping");
        cut.Find(".tag-field-add").Click();
        cut.Find(".tag-field-input").Input("Car");
        ClickButtonSaying(cut, "Save");

        // The second was never added, and is saved all the same.
        Assert.Contains("\"categories\":[\"shopping\",\"Car\"]", _lastSavedJson);
    }

    [Fact]
    public void A_word_already_on_the_row_is_not_added_twice()
    {
        RegisterApiClients(AnItem());
        var cut = Render();
        ExpandTheOnlyItem(cut);

        cut.Find(".tag-field-input").Input("shopping");
        cut.Find(".tag-field-add").Click();
        cut.Find(".tag-field-input").Input("Shopping");
        ClickButtonSaying(cut, "Save");

        Assert.Contains("\"categories\":[\"shopping\"]", _lastSavedJson);
    }

    [Fact]
    public void An_entry_already_filed_shows_what_it_is_filed_under()
    {
        RegisterApiClients(AnItem() with { Categories = ["shopping", "car"] });
        var cut = Render();
        ExpandTheOnlyItem(cut);

        // Chips rather than a line of text: what is already filed is a set of things, and the box below
        // them is empty and ready for the next one.
        Assert.Equal(["shopping", "car"], cut.FindAll(".tag-chip").Select(chip => chip.TextContent.Replace("✕", string.Empty).Trim()));
        Assert.Equal(string.Empty, cut.Find(".tag-field-input").GetAttribute("value"));
    }

    /// <summary>
    /// A list measured against a storage can put something on it: the entry describes a product, and
    /// its own words are that product's name - so the form asks everything except the name. Without
    /// this, such a list could only be told to generate a second storage it was not allowed to have.
    /// </summary>
    [Fact]
    public void An_inventory_entry_on_a_measured_list_describes_a_product_for_that_shelf()
    {
        _linkedInventory = new InventoryDto(
            Guid.NewGuid(), "Pantry", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", LockedByUserName: null,
            OriginalOwnerUserId: null);
        RegisterApiClients(AnItem(kind: nameof(TaskItemKind.Inventory)));
        var cut = Render();

        ExpandTheOnlyItem(cut);

        var details = cut.Find(".editor-item-details").TextContent;
        // The amounts are asked for, the name is not - the entry above is the name.
        Assert.Contains("Amount", details);
        Assert.DoesNotContain("Item name", details);
        Assert.Contains("Pantry", details);

        // And it starts where generating a storage from a list would have put it: none of it there yet,
        // counted in pieces, and no minimum - which is what leaves the counting rule to answer, so a
        // thing named three times still asks for three. See TaskItemProduct.Default.
        var amounts = cut.FindAll(".editor-item-details input[type=number]").ToList();
        Assert.Null(amounts[1].GetAttribute("value"));
        Assert.Equal("Piece", cut.Find(".editor-item-unit").GetAttribute("value"));
    }

    /// <summary>
    /// An entry cannot link to the list it belongs to, so a list it already stands for is not somewhere
    /// it can be moved - and the server refuses such a move with a 400. Left out of the dropdown rather
    /// than offered and then rejected.
    /// </summary>
    [Fact]
    public void A_list_an_entry_already_stands_for_is_not_offered_as_somewhere_to_move_it()
    {
        RegisterApiClients(AnItem() with { LinkedTaskListIds = [OtherTaskListId] });
        var cut = Render();
        ExpandTheOnlyItem(cut);

        var offered = cut.FindAll("select")
            .SelectMany(select => select.QuerySelectorAll("option"))
            .Select(option => option.GetAttribute("value"));
        Assert.DoesNotContain(OtherTaskListId.ToString(), offered);
    }

    [Fact]
    public void What_the_list_is_for_is_shown_under_its_title()
    {
        RegisterApiClients(AnItem());

        var cut = Render();

        Assert.Equal(["Errands", "Things to pick up on the way home"], WhatTheFieldHolds(cut));
    }

    /// <summary>
    /// A field the form shows but does not send back looks saved and is gone on the next load. The save
    /// builds a fresh request object, which is exactly where this app has lost fields before.
    /// </summary>
    [Fact]
    public void And_goes_back_with_the_save()
    {
        RegisterApiClients(AnItem());
        var cut = Render();

        WriteIntoTheField(cut, "Errands", "Only what the shop is out of");
        ClickButtonSaying(cut, "Save");

        Assert.NotNull(_lastSavedJson);
        Assert.Contains("Only what the shop is out of", _lastSavedJson);
    }


    /// <summary>
    /// An entry can be pointed at several lists, and every one of them has to reach the save. The
    /// picker adds one at a time and the chosen ones are listed underneath, so the reader can see what
    /// the entry stands for without opening a dropdown.
    /// </summary>
    [Fact]
    public void An_entry_can_be_made_to_stand_for_two_lists_and_both_are_saved()
    {
        RegisterApiClients(AnItem());
        var cut = Render();
        ExpandTheOnlyItem(cut);

        var picker = cut.FindAll("select").Single(box => box.GetAttribute("aria-label") == "Stands for these lists");
        var choices = picker.QuerySelectorAll("option")
            .Select(option => option.GetAttribute("value"))
            .Where(value => !string.IsNullOrEmpty(value))
            .Take(2)
            .ToList();
        Assert.Equal(2, choices.Count);

        picker.Change(choices[0]);
        cut.FindAll("select").Single(box => box.GetAttribute("aria-label") == "Stands for these lists").Change(choices[1]);
        ClickButtonSaying(cut, "Save");

        Assert.NotNull(_lastSavedJson);
        Assert.Contains(choices[0]!, _lastSavedJson);
        Assert.Contains(choices[1]!, _lastSavedJson);
    }

    /// <summary>A list already named is not offered again - that would be offering to say it twice.</summary>
    [Fact]
    public void A_list_it_already_stands_for_is_not_offered_again()
    {
        RegisterApiClients(AnItem());
        var cut = Render();
        ExpandTheOnlyItem(cut);

        var picker = cut.FindAll("select").Single(box => box.GetAttribute("aria-label") == "Stands for these lists");
        var chosen = picker.QuerySelectorAll("option")
            .Select(option => option.GetAttribute("value"))
            .First(value => !string.IsNullOrEmpty(value));
        picker.Change(chosen);

        var offeredAfterwards = cut.FindAll("select")
            .Single(box => box.GetAttribute("aria-label") == "Stands for these lists")
            .QuerySelectorAll("option")
            .Select(option => option.GetAttribute("value"));
        Assert.DoesNotContain(chosen, offeredAfterwards);
    }

    /// <summary>
    /// What an entry asks for is written on the entry and saved with the list, so it survives until
    /// there is a shelf to put it on - see Orbit.Core.Tasks.TaskItemProduct. Before this, an entry named
    /// a thing and nothing else, and everything about that thing had to be typed again afterwards.
    /// </summary>
    [Fact]
    public void What_an_inventory_entry_asks_for_is_saved_with_the_list()
    {
        RegisterApiClients(AnItem(kind: nameof(TaskItemKind.Inventory)));
        var cut = Render();
        ExpandTheOnlyItem(cut);

        // Typed rather than committed on leaving the box: it is a SuggestedTextField now, and the panel
        // under it keeps up with what is being written - see InventoryFields.
        cut.FindAll("input").First(box => box.GetAttribute("placeholder") == "Product type").Input("Dry goods");
        cut.Find(".editor-item-unit").Change("Kilogram");
        ClickButtonSaying(cut, "Save");

        Assert.Contains("\"productType\":\"Dry goods\"", _lastSavedJson);
        Assert.Contains("\"unit\":\"Kilogram\"", _lastSavedJson);
    }

    /// <summary>
    /// One box, not two. The entry has always had a categories box; the product form an Inventory entry
    /// opens brought a second one, directly under it, with the same label and the same placeholder -
    /// and nothing on the screen said which was which. See InventoryFields.ShowsCategories.
    /// </summary>
    [Fact]
    public void An_inventory_entry_asks_what_it_is_filed_under_once()
    {
        RegisterApiClients(AnItem(kind: nameof(TaskItemKind.Inventory)));
        var cut = Render();

        ExpandTheOnlyItem(cut);

        var details = cut.Find(".editor-item-details");
        Assert.Single(details.QuerySelectorAll(".editor-item-tags"));
        // The rest of the product form is still there - this took a box away, not the form.
        Assert.Contains("Product type", details.TextContent);
    }

    /// <summary>
    /// And that one box answers for both: the words on the entry are what the row it builds is filed
    /// under, so nobody types "food" twice - see TaskEditor.ProductAsked.
    /// </summary>
    [Fact]
    public void What_an_inventory_entry_is_filed_under_is_what_its_product_is_filed_under()
    {
        RegisterApiClients(AnItem(kind: nameof(TaskItemKind.Inventory)));
        var cut = Render();
        ExpandTheOnlyItem(cut);

        cut.Find(".tag-field-input").Input("Dry goods");
        cut.Find(".tag-field-add").Click();
        ClickButtonSaying(cut, "Save");

        Assert.Contains("\"categories\":[\"Dry goods\"]", _lastSavedJson);
        // Twice over: once on the entry, and once inside what the entry asks for.
        Assert.Equal(
            2, _lastSavedJson!.Split("\"categories\":[\"Dry goods\"]").Length - 1);
    }

    /// <summary>
    /// The other way round, for an entry saved before there was one box: what its product was filed
    /// under is what the box opens showing. Without this the box would open empty and the save would
    /// write that emptiness back over what was there - see TaskEditor.CategoriesOf.
    /// </summary>
    [Fact]
    public void An_inventory_entry_filed_only_on_its_product_shows_those_words()
    {
        RegisterApiClients(AnItem(kind: nameof(TaskItemKind.Inventory)) with
        {
            Categories = [],
            Product = new TaskItemProductDto(
                "Food", ["Dry goods"], Quantity: 0, MinimumQuantity: null, Unit: "Piece", ExpiryDate: null,
                ExpiryNotificationChannel: "None", IsCheckedRegularly: false)
        });
        var cut = Render();

        ExpandTheOnlyItem(cut);

        Assert.Equal(
            ["Dry goods"],
            cut.FindAll(".tag-chip").Select(chip => chip.TextContent.Replace("✕", string.Empty).Trim()));
    }

    /// <summary>A storage for the two tests that watch what a save writes back to a shelf.</summary>
    private void MeasuredAgainstAStorage()
        => _linkedInventory = new InventoryDto(
            Guid.NewGuid(), "Pantry", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", LockedByUserName: null,
            OriginalOwnerUserId: null);

    private static InventoryItemDto AShelfRow(string name, string category, params string[] categories)
        => new(
            Guid.NewGuid(), name, "Dry goods", category, Quantity: 1, MinimumQuantity: 1, Unit: "Piece",
            ExpiryDate: null, ExpiryNotificationChannel: "None", IsBelowMinimum: false,
            HasPendingRestockTask: false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsCheckedRegularly: false, Categories: categories);

    /// <summary>
    /// The one box, all the way to the shelf: what the entry is filed under is what the row it puts on
    /// that shelf is filed under. Before this the product form asked again, in a box that looked exactly
    /// like the entry's, and the answer typed in the visible one never reached the storage.
    /// </summary>
    [Fact]
    public void An_entrys_categories_are_what_its_new_shelf_row_is_filed_under()
    {
        MeasuredAgainstAStorage();
        RegisterApiClients(AnItem(kind: nameof(TaskItemKind.Inventory)));
        var cut = Render();
        ExpandTheOnlyItem(cut);

        cut.Find(".tag-field-input").Input("Dry goods");
        cut.Find(".tag-field-add").Click();
        ClickButtonSaying(cut, "Save");

        Assert.Contains("\"name\":\"Buy milk\"", _lastShelfJson);
        Assert.Contains("\"categories\":[\"Dry goods\"]", _lastShelfJson);
    }

    /// <summary>
    /// A row this list never touched still travels through the save, and it has to come out filed under
    /// everything it went in filed under. Leaving the list of words unsaid means "the sender does not
    /// know about them", and the single old field was then read as the whole answer - so saving a task
    /// list quietly filed a two-word row under one. See TaskEditor.SaveTheShelfAsync.
    /// </summary>
    [Fact]
    public void A_shelf_row_this_list_never_touched_keeps_every_word_it_is_filed_under()
    {
        MeasuredAgainstAStorage();
        _shelf = [AShelfRow("Mąka", "Baking", "Baking", "Dry goods")];
        RegisterApiClients(AnItem(kind: nameof(TaskItemKind.Inventory)));
        var cut = Render();
        // Opened, because that is what puts this list's own entry on the shelf being written - a list
        // nobody opened an inventory entry on writes no shelf at all. See TaskEditor.ShelfFieldsFor.
        ExpandTheOnlyItem(cut);

        ClickButtonSaying(cut, "Save");

        Assert.Contains("\"categories\":[\"Baking\",\"Dry goods\"]", _lastShelfJson);
    }

    /// <summary>An entry of another kind describes nothing, and says nothing about it - see TaskEditor.ProductAsked.</summary>
    [Fact]
    public void An_ordinary_entry_says_nothing_about_a_product()
    {
        RegisterApiClients(AnItem());
        var cut = Render();

        ClickButtonSaying(cut, "Save");

        Assert.Contains("\"product\":null", _lastSavedJson);
    }

    /// <summary>
    /// Generating a storage asks what to build first: what it is called, and how the "Restock supplies"
    /// list it keeps should behave. It used to be one click with no questions, and both answers then had
    /// to be found and corrected on another screen.
    /// </summary>
    [Fact]
    public void Generating_a_storage_asks_what_to_build()
    {
        RegisterApiClients(AnItem(kind: nameof(TaskItemKind.Inventory)));
        var cut = Render();

        OpenTheRailMenu(cut);
        ClickButtonSaying(cut, "Generate inventory…");

        Assert.NotEmpty(cut.FindAll(".form-overlay"));
        // Nothing is built until the form is answered.
        Assert.Null(_lastGenerateJson);
    }

    [Fact]
    public void What_the_form_answers_is_what_gets_built()
    {
        RegisterApiClients(AnItem(kind: nameof(TaskItemKind.Inventory)));
        var cut = Render();

        OpenTheRailMenu(cut);
        ClickButtonSaying(cut, "Generate inventory…");
        cut.Find("#generatedInventoryName").Input("Pantry");
        cut.Find("#generatedRestockChannel").Change("Email");
        cut.Find("#generatedRestockScope").Change("true");
        ClickButtonSaying(cut, "Generate");

        Assert.Contains("\"name\":\"Pantry\"", _lastGenerateJson);
        Assert.Contains("\"reminderChannel\":\"Email\"", _lastGenerateJson);
        Assert.Contains("\"onlyCheckedRegularly\":true", _lastGenerateJson);
        // And the list is written first, so the storage is built from what is on screen rather than from
        // what was stored before somebody filled the entries in - see GenerateInventoryAsync.
        Assert.NotNull(_lastSavedJson);
    }

    /// <summary>
    /// The name box is left empty rather than pre-filled: an untouched box shows the list's title as its
    /// placeholder, and a blank name is what the server reads as "call it what the list is called".
    /// </summary>
    [Fact]
    public void The_storage_is_offered_the_lists_own_name()
    {
        RegisterApiClients(AnItem(kind: nameof(TaskItemKind.Inventory)));
        var cut = Render();

        OpenTheRailMenu(cut);
        ClickButtonSaying(cut, "Generate inventory…");

        Assert.Equal("Errands", cut.Find("#generatedInventoryName").GetAttribute("placeholder"));
    }

    /// <summary>
    /// The name and what is under it, as the one field they are written in holds them - see
    /// TitledDescription. Read through the surface rather than off the DOM: it is contenteditable driven
    /// from JS, which a test renderer has none of.
    /// </summary>
    private static string[] WhatTheFieldHolds(IRenderedFragment cut)
        => [.. cut.FindComponent<ChecklistTextEditor>().Instance.Lines.Select(line => line.Text)];

    /// <summary>What that field reports after somebody types into it, called the way its own JS calls it.</summary>
    private static void WriteIntoTheField(IRenderedFragment cut, params string[] lines)
    {
        var editor = cut.FindComponent<ChecklistTextEditor>().Instance;
        var written = lines.Select(line => new NoteContentLineDto(line, IsChecklistItem: false, IsChecked: false));
        cut.InvokeAsync(() => editor.OnLinesChangedFromJs(
            JsonSerializer.Serialize(written, new JsonSerializerOptions(JsonSerializerDefaults.Web))))
            .GetAwaiter().GetResult();
    }

    private IRenderedComponent<TaskEditor> Render()
        => RenderComponent<TaskEditor>(parameters => parameters.Add(editor => editor.Id, TaskListId));

    private static void ExpandTheOnlyItem(IRenderedFragment cut) => cut.Find(".editor-item-toggle").Click();

    /// <summary>
    /// The three-dot menu in the rail, where everything about the list as a whole lives - see
    /// OverflowMenu, which draws nothing until it is opened.
    /// </summary>
    private static void OpenTheRailMenu(IRenderedFragment cut) => cut.Find(".overflow-menu-trigger").Click();

    private static void ClickButtonSaying(IRenderedFragment cut, string label)
        => ButtonSaying(cut, label).Click();

    /// <summary>
    /// A button by what it says - its words, or the name it carries for a screen reader, since an
    /// editor's Save and Cancel are icons now (see EditorRail.razor). The screen-reader name is looked
    /// at first and matched whole: a page can hold both the editor's Save and a "Save settings" beside
    /// something else, and by their words alone the wrong one answers to "Save".
    /// </summary>
    private static AngleSharp.Dom.IElement ButtonSaying(IRenderedFragment cut, string label)
        => cut.FindAll("button").FirstOrDefault(button =>
               string.Equals(button.GetAttribute("aria-label"), label, StringComparison.Ordinal))
            ?? cut.FindAll("button").First(button => button.TextContent.Contains(label, StringComparison.Ordinal));

    private static TaskItemDto AnItem(
        string kind = nameof(TaskItemKind.Checklist), DateTimeOffset? dueDateUtc = null, bool remindDaily = false,
        TimeOnly dailyReminderTimeOfDay = default)
        => new(
            ItemId, "Buy milk", dueDateUtc, IsCompleted: false, LinkedTaskListId: null,
            OverdueNotificationChannel: "None", remindDaily, DailyReminderNotificationChannel: "Push",
            dailyReminderTimeOfDay, kind);

    private string? _lastSavedJson;

    /// <summary>What the page asked the server to build, when it asked - see GenerateInventoryOverlay.</summary>
    private string? _lastGenerateJson;

    /// <summary>What the save wrote back to the storage this list is measured against - see SaveTheShelfAsync.</summary>
    private string? _lastShelfJson;

    /// <summary>What that storage already holds. Empty for the tests that only look at the list.</summary>
    private IReadOnlyList<InventoryItemDto> _shelf = [];

    private static readonly Guid GeneratedInventoryId = Guid.NewGuid();

    private void RegisterApiClients(TaskItemDto item)
    {
        var taskList = new TaskDto(
            TaskListId, "Errands", [item], IsCompleted: false, IsGroup: false, IsPrivate: false,
            EncryptedContent: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null,
            Description: "Things to pick up on the way home",
            LinkedInventoryId: _linkedInventory?.Id);

        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Put && path.EndsWith($"/{TaskListId}", StringComparison.Ordinal))
            {
                _lastSavedJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            // Turning this list into a storage - what the form in the rail's menu asks for before
            // anything is built. See GenerateInventoryOverlay.
            if (request.Method == HttpMethod.Post && path.EndsWith("/inventory", StringComparison.Ordinal))
            {
                _lastGenerateJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonOf(GeneratedInventoryId);
            }

            if (path.Contains("/notifications", StringComparison.Ordinal))
            {
                return Json(new NotificationSettingsDto(
                    true, true, true, true, ShowExceptionDetails: false,
                    BannerVisibleSeconds: 5, BannerMinimumGapSeconds: 5));
            }

            // Somebody a calendar entry could invite, so "no contacts" in the guest picker means the
            // page did not ask rather than that there was nobody to ask about.
            if (path.EndsWith("/chat/contacts", StringComparison.Ordinal))
            {
                return Json(new[]
                {
                    new ContactDto(
                        Guid.NewGuid(), "anna", "Anna Kowalska", "anna@example.com", "public-key",
                        DateTimeOffset.UtcNow, RequiresApprovalFromCurrentUser: false,
                        IsPendingApprovalFromOtherParty: false)
                });
            }

            // The references route answers what shelf items this list's errands are about; these lists
            // carry none. Everything else that is asked for here is a collection nobody asserts on.
            if (path.EndsWith("/inventory-references", StringComparison.Ordinal)
                || path.Contains("/calendar", StringComparison.Ordinal)
                || path.Contains("/chat", StringComparison.Ordinal))
            {
                return Json(Array.Empty<object>());
            }

            if (path.StartsWith("/api/share-links", StringComparison.Ordinal) || path.EndsWith("/lock", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            // No storages, so the picker under "About this list" has nothing to offer. Answered
            // explicitly because the fallback below hands back task lists, and an inventory and a task
            // list are close enough in shape to be read as one another.
            if (path.EndsWith("/api/inventories", StringComparison.Ordinal))
            {
                return JsonOf(_linkedInventory is null ? Array.Empty<InventoryDto>() : [_linkedInventory]);
            }

            if (_linkedInventory is { } storage && path.EndsWith($"/api/inventories/{storage.Id}", StringComparison.Ordinal))
            {
                // The shelf is written back by the same save that writes the list - see SaveTheShelfAsync.
                if (request.Method == HttpMethod.Put)
                {
                    _lastShelfJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }

                return JsonOf(storage);
            }

            if (_linkedInventory is { } shelved && path.EndsWith($"/api/inventories/{shelved.Id}/items", StringComparison.Ordinal))
            {
                return JsonOf(_shelf);
            }

            if (path.EndsWith("/restock-list/refresh", StringComparison.Ordinal))
            {
                return JsonOf(new RestockRefreshResultDto(0, 0));
            }

            // The storage the generation just built, which the page reads back to name it on screen.
            if (path.EndsWith($"/api/inventories/{GeneratedInventoryId}", StringComparison.Ordinal))
            {
                return JsonOf(new InventoryDto(
                    GeneratedInventoryId, "Spiżarnia", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                    IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", LockedByUserName: null,
                    OriginalOwnerUserId: null));
            }

            // Two other lists as well, so the "stands for these lists" picker has something to offer -
            // it never offers the list being edited, which would be a link to itself.
            return path.EndsWith($"/{TaskListId}", StringComparison.Ordinal)
                ? JsonOf(taskList)
                : JsonOf(new[] { taskList, AnotherTaskList("Kitchen", OtherTaskListId), AnotherTaskList("Bathroom") });
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        Services.AddSingleton(new TasksApiClient(httpClient));
        Services.AddSingleton(new ChatApiClient(httpClient));
        Services.AddSingleton(new CalendarApiClient(httpClient));
        Services.AddSingleton(new NotificationsApiClient(httpClient));
        Services.AddSingleton(new PublicShareApiClient(httpClient));
        Services.AddSingleton(new InventoryApiClient(httpClient));
    }

    private static TaskDto AnotherTaskList(string title, Guid? id = null)
        => new(
            id ?? Guid.NewGuid(), title, [], IsCompleted: false, IsGroup: false, IsPrivate: false,
            EncryptedContent: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    private static HttpResponseMessage Json(object body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private static HttpResponseMessage JsonOf<T>(T body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private void RegisterAuthentication()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        tokenStore.SetTokenAsync(CreateUnsignedJwt()).GetAwaiter().GetResult();
        var refreshHttpClient = new HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var provider = new OrbitAuthenticationStateProvider(tokenStore, new TokenRefreshService(tokenStore, refreshHttpClient));
        Services.AddSingleton(provider);
        Services.AddSingleton<AuthenticationStateProvider>(provider);
        Services.AddAuthorizationCore();

        // The editor injects the chat sender for the sharing block, whether or not that block renders.
        var jsRuntime = JSInterop.JSRuntime;
        var usersApiClient = new UsersApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") });
        Services.AddSingleton(usersApiClient);
        Services.AddSingleton(new EncryptedChatMessageSender(
            jsRuntime,
            new OwnEncryptionKeyProvider(jsRuntime, usersApiClient, provider),
            usersApiClient,
            new ChatApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") })));
    }

    private void RegisterPermissions()
    {
        // Nothing granted: these tests are about an entry's own form, and the Sharing block below it
        // pulls in the chat stack, which has nothing to do with what is being asserted.
        var permissions = new UserPermissionState(new UsersApiClient(
            new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"granted\":[]}", Encoding.UTF8, "application/json")
            }))
            {
                BaseAddress = new Uri("https://example.test/")
            }));
        permissions.EnsureLoadedAsync().GetAwaiter().GetResult();
        Services.AddSingleton(permissions);
    }

    private static string CreateUnsignedJwt()
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(
            $$"""{"sub":"{{Guid.NewGuid()}}","email":"owner@example.com","name":"Test Owner"}"""));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
