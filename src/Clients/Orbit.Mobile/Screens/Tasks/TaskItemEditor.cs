using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Tasks;
using Orbit.Core.Tasks;
using Orbit.Core.Notifications;
using Orbit.Core.Suggestions;
using Orbit.Mobile.Screens.Inventory;
using Orbit.Mobile.Screens.Suggestions;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// One task-list entry while it is being edited. Everything <see cref="TaskItemDto"/> carries and the
/// phone could not reach: when it is due, what happens when it goes overdue, whether it says something
/// every day until it is done, and - for an entry that is somewhere to be rather than something to
/// fetch - what kind it is, where it happens and which event it belongs to.
///
/// A separate object from the DTO for the same reason as InventoryItemEditor: a form holds half-typed
/// values, and "no due date" and "a date being picked" are different states the DTO cannot express.
/// </summary>
public sealed partial class TaskItemEditor : ObservableObject
{
    /// <summary>
    /// What the web's dropdown offers, in the same order and with the same wording - see
    /// NotificationChannelChoice. Taken in the factory rather than set afterwards: the picker reads it
    /// once, when the form appears, and a list handed over after that is never looked at.
    /// </summary>
    public IReadOnlyList<NotificationChannelChoice> Channels { get; private init; } = [];

    /// <summary>Bound to the picker, which needs a choice out of Channels rather than a string.</summary>
    public NotificationChannelChoice? ChosenOverdueNotificationChannel
    {
        get => NotificationChannelChoice.For(Channels, OverdueNotificationChannel);
        set
        {
            if (value is not null)
            {
                OverdueNotificationChannel = value.Value;
            }
        }
    }

    /// <summary>Bound to the picker, which needs a choice out of Channels rather than a string.</summary>
    public NotificationChannelChoice? ChosenDailyReminderNotificationChannel
    {
        get => NotificationChannelChoice.For(Channels, DailyReminderNotificationChannel);
        set
        {
            if (value is not null)
            {
                DailyReminderNotificationChannel = value.Value;
            }
        }
    }

    /// <summary>What an entry can be - see <see cref="TaskItemKindChoice"/>.</summary>
    public IReadOnlyList<TaskItemKindChoice> Kinds { get; private init; } = [];

    /// <summary>Bound to the picker, which needs a choice out of Kinds rather than a string.</summary>
    public TaskItemKindChoice? ChosenKind
    {
        get => TaskItemKindChoice.For(Kinds, Kind);
        set
        {
            if (value is not null)
            {
                Kind = value.Value;
            }
        }
    }

    /// <summary>
    /// The appointment this entry is - see <see cref="TaskItemEventForm"/>. Present on every entry, not
    /// only a Calendar one, so switching kinds back and forth does not lose what was typed.
    /// </summary>
    public TaskItemEventForm Event { get; private init; } = null!;

    /// <summary>
    /// The event this entry already has in the calendar, or null when saving it will make one. Carried
    /// through untouched: it is what tells an update from a creation, and nothing on this screen sets it.
    /// </summary>
    public Guid? LinkedCalendarEventId { get; private init; }

    /// <summary>
    /// The product an Inventory entry is an errand about - see <see cref="TaskItemShelfProduct"/>. Null
    /// for every other kind, and for an errand whose product this phone has not got: an entry tied to a
    /// inventory somebody stopped sharing still opens, with the shelf half missing rather than the form.
    /// </summary>
    /// <summary>
    /// The product this errand is about - one already on a shelf, or one being described for the shelf
    /// the list is measured against. Settable because the answer follows the kind: somebody who picks
    /// Inventory here means "this is an errand about a product" and the fields for it have to appear
    /// then, not after a save and a reopen.
    /// </summary>
    public TaskItemShelfProduct? Shelf { get; private set; }

    /// <summary>
    /// Makes the form for a product this shelf has not got yet, or null when the list is measured
    /// against no shelf. Handed in rather than reached for, the way the channels and the lists are -
    /// which inventory a list is measured against is the screen's knowledge, not the editor's.
    /// </summary>
    public Func<TaskItemShelfProduct?>? ShelfForSomethingNew { private get; init; }

    /// <summary>True when this entry is an errand about a product that can be corrected from here.</summary>
    public bool IsShelfEntry => Kind == nameof(TaskItemKind.Inventory) && Shelf is not null;

    /// <summary>
    /// What this entry asks for while no shelf holds it - the same product form, kept on the entry
    /// itself (<see cref="TaskItemDto.Product"/>) until "Generate inventory" turns each of these into a
    /// row. Built once, whatever the kind, so switching kinds back and forth does not lose what was
    /// typed - the same reason <see cref="Event"/> is always there.
    ///
    /// Null only for an entry that already stands for a real shelf row: the row is then the answer, and
    /// the server ignores anything the entry says about a product beside it (see TaskItem.Product).
    /// </summary>
    public InventoryItemEditor? ProductWanted { get; private init; }

    /// <summary>
    /// Whether that form is what this entry shows. An errand on a list measured against a shelf shows
    /// <see cref="Shelf"/> instead - the product goes there when the list is saved rather than waiting
    /// for a storage to be generated - and every other kind shows neither.
    /// </summary>
    public bool IsAskingForSomethingNoShelfHasYet
        => Kind == nameof(TaskItemKind.Inventory) && Shelf is null && ProductWanted is not null;

    /// <inheritdoc cref="IsAskingForSomethingNoShelfHasYet"/>
    public string WhatBecomesOfWhatItAsksFor
        => _translations["Goes on the shelf when \"Generate inventory\" builds one, named after this entry. Leave the minimum empty to have it counted instead: the same name twice asks for two of it."];

    /// <summary>
    /// Said before anything is changed: this form writes to an inventory, not only to the list. Empty
    /// when there is no product behind the entry.
    /// </summary>
    public string WhereTheProductLives
        => Shelf is null
            ? string.Empty
            : Shelf.Product.IsSomethingNew
                ? _translations.Format(
                    "Goes on the shelf in {0} when this entry is saved, named after this entry.",
                    Shelf.InventoryName)
                : _translations.Format(
                    "On the shelf in {0}. Saving this list saves the change there too.", Shelf.InventoryName);

    /// <summary>
    /// Whether the form is describing something to put on the shelf rather than correcting what is
    /// already there. The name box is left out for it: the entry's own words are the product's name,
    /// and two boxes for one name is two answers to the same question.
    /// </summary>
    public bool IsDescribingSomethingNew => Shelf is { Product.IsSomethingNew: true };

    /// <summary>
    /// An Inventory entry with nothing behind it - one that names a row on a shelf this phone has not
    /// got, because the inventory is gone or has not synced. Said rather than left as an empty form
    /// that looks broken, which is the line Orbit.Web draws too. An entry naming no row at all is not
    /// this case: it describes what it wants instead - see <see cref="IsAskingForSomethingNoShelfHasYet"/>.
    /// </summary>
    public bool HasNoProductToEdit
        => Kind == nameof(TaskItemKind.Inventory) && Shelf is null && ProductWanted is null;

    /// <inheritdoc cref="HasNoProductToEdit"/>
    public string NoProductMessage
        => _translations["This entry isn't tied to a product yet, so there is nothing to edit here."];

    /// <summary>
    /// Lists this entry can be made to stand for, the first of them being "none" - see
    /// TaskListChoice.NoList. A group list gathers other lists through entries that point at them.
    /// </summary>
    public IReadOnlyList<TaskListChoice> LinkableTaskLists { get; private init; } = [];


    /// <summary>
    /// Every list this entry stands for, in the order they were added. An entry that points somewhere is
    /// ticked by the lists it names rather than by hand, which is why the tick is left to them.
    /// </summary>
    public ObservableCollection<TaskListChoice> LinkedTaskLists { get; } = [];

    /// <summary>What the picker offers: the lists this entry does not already stand for.</summary>
    public IReadOnlyList<TaskListChoice> LinkableTaskListsLeft
        => [.. LinkableTaskLists.Where(choice =>
            choice.ServerId is null || LinkedTaskLists.All(linked => linked.ServerId != choice.ServerId))];

    /// <summary>Whether anything is named at all, which is what the row of names hangs off.</summary>
    public bool IsALinkToOtherLists => LinkedTaskLists.Count > 0;

    /// <summary>Nothing to point at is nothing to offer - a phone with one list needs no picker.</summary>
    public bool CanBeLinked => LinkableTaskLists.Count > 1;

    /// <summary>
    /// Makes the entry stand for one more list. Adding rather than replacing: one entry often stands for
    /// several, which is what the web gained on 2026-09-01 and the phone could only carry.
    ///
    /// A command rather than the picker's own bound value. Choosing a list changes what the picker
    /// offers and what it has selected, and doing either from inside the picker's own change - which a
    /// bound property does - hung the app on Android: the dialog stopped answering and the screen was
    /// reported as not responding. The head now says "this was chosen" and settles the picker itself,
    /// after its selection has finished - see TaskListDetailPage.OnLinkedTaskListPicked.
    /// </summary>
    [RelayCommand]
    private void LinkTo(TaskListChoice? chosen)
    {
        if (chosen?.ServerId is null || LinkedTaskLists.Any(linked => linked.ServerId == chosen.ServerId))
        {
            return;
        }

        LinkedTaskLists.Add(chosen);
        SayWhatItStandsFor();
    }

    /// <summary>Takes one list off the entry. The others stay: it may stand for several.</summary>
    [RelayCommand]
    private void Unlink(TaskListChoice? linked)
    {
        if (linked is not null && LinkedTaskLists.Remove(linked))
        {
            SayWhatItStandsFor();
        }
    }

    private void SayWhatItStandsFor()
    {
        OnPropertyChanged(nameof(IsALinkToOtherLists));
        OnPropertyChanged(nameof(LinkableTaskListsLeft));
    }

    /// <summary>
    /// The other entries of this same list, which this one can be made to wait for - "hang the door"
    /// after "fit the hinges". Handed in by the screen, the way the linkable lists are: which entries
    /// are on the list is the screen's knowledge, not this form's.
    ///
    /// A different thing from the lists above: that is one entry meaning whole other lists, this is the
    /// order the work on this one has to be done in. See Orbit.Core.Tasks.TaskListSteps, which is where
    /// the rule lives - and which reads it off every entry whatever its kind, so this is offered on all
    /// three the way the browser now offers it.
    /// </summary>
    public IReadOnlyList<TaskEntryChoice> WaitableEntries { get; private init; } = [];

    /// <summary>Every entry this one waits for, in the order they were added.</summary>
    public ObservableCollection<TaskEntryChoice> WaitsFor { get; } = [];

    /// <summary>What the picker offers: the entries this one does not already wait for.</summary>
    public IReadOnlyList<TaskEntryChoice> WaitableEntriesLeft
        => [.. WaitableEntries.Where(choice => WaitsFor.All(step => step.Id != choice.Id))];

    /// <summary>Whether anything is named at all, which is what the row of names hangs off.</summary>
    public bool WaitsForAnything => WaitsFor.Count > 0;

    /// <summary>
    /// Nothing to wait for is nothing to offer - a list with one entry on it, and an entry that has not
    /// been saved yet, both have nobody to queue behind.
    /// </summary>
    public bool CanWaitForAnything => WaitableEntries.Count > 0;

    /// <summary>
    /// Puts this entry behind one more. A command rather than the picker's own bound value, and for the
    /// reason LinkTo above gives: rebuilding what a picker offers from inside its own change hung the
    /// app on Android. See TaskListDetailPage.OnStepPicked.
    /// </summary>
    [RelayCommand]
    private void WaitFor(TaskEntryChoice? chosen)
    {
        if (chosen is null || WaitsFor.Any(step => step.Id == chosen.Id))
        {
            return;
        }

        WaitsFor.Add(chosen);
        SayWhatItWaitsFor();
    }

    /// <summary>Takes one step off the entry. The others stay: it may wait for several.</summary>
    [RelayCommand]
    private void StopWaitingFor(TaskEntryChoice? step)
    {
        if (step is not null && WaitsFor.Remove(step))
        {
            SayWhatItWaitsFor();
        }
    }

    private void SayWhatItWaitsFor()
    {
        OnPropertyChanged(nameof(WaitsForAnything));
        OnPropertyChanged(nameof(WaitableEntriesLeft));
    }

    private readonly TaskItemDto _item;

    /// <summary>
    /// Only a calendar entry has somewhere to be, so the place and the tie to an event are shown for one
    /// and hidden for the rest - the same thing Orbit.Web's editor does with them.
    /// </summary>
    public bool IsCalendarEntry => Kind == nameof(TaskItemKind.Calendar);

    /// <summary>
    /// An entry that is a place and nothing else: "pick the keys up from the agent, here". It keeps a
    /// place without keeping an hour, which is what separates it from a calendar entry.
    /// </summary>
    public bool IsPlaceEntry => Kind == nameof(TaskItemKind.Location);

    /// <summary>
    /// Where an entry happens, asked on the entry rather than on its appointment. The calendar's
    /// own location is coordinates first and an entry carries only a name, so the two are not the same
    /// field - which is why Orbit.Web leaves the name here and sends the event none.
    /// </summary>
    public bool CanSayWhereItHappens => IsCalendarEntry || IsPlaceEntry;

    private readonly Translations _translations;

    [ObservableProperty]
    private string _kind = nameof(TaskItemKind.Checklist);

    /// <summary>Where a calendar entry happens, as somebody typed it - see TaskItemDto.Location.</summary>
    [ObservableProperty]
    private string _location = string.Empty;

    /// <summary>
    /// Where that place actually is, once it is known - from a pin, or from looking the typed name up.
    /// The name alone cannot be saved: an entry that has an appointment keeps no place of its own (see
    /// Orbit.Core's TaskItem.WhereItHappens), so the place has to go on the appointment, and an
    /// appointment stores a point first.
    /// </summary>
    public double? LocationLatitude { get; set; }

    /// <inheritdoc cref="LocationLatitude"/>
    public double? LocationLongitude { get; set; }

    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>
    /// What this entry is about, in as many words as it takes - see TaskItemDto.Notes. One box for every
    /// kind, and for a Calendar entry it is the appointment's description too: the event's own form
    /// leaves its copy out, and this is written onto it when the entry is saved - see
    /// TaskListDetailViewModel.SaveItemAsync, which draws the line where Orbit.Web's EventDetailsFor does.
    /// </summary>
    [ObservableProperty]
    private string _notes = string.Empty;

    /// <summary>
    /// What the entry is about, as many as apply, on one line and separated by commas - the same box
    /// the browser offers and the same rule behind it, see CategoryText. The tasks screen looks for an
    /// entry among every list by these.
    /// </summary>
    [ObservableProperty]
    private string _categories = string.Empty;

    [ObservableProperty]
    private bool _hasDueDate;

    [ObservableProperty]
    private DateTime _dueDate = DateTime.Today;

    [ObservableProperty]
    private string _overdueNotificationChannel = nameof(NotificationChannel.None);

    [ObservableProperty]
    private bool _remindDaily;

    [ObservableProperty]
    private string _dailyReminderNotificationChannel = nameof(NotificationChannel.None);

    [ObservableProperty]
    private TimeSpan _dailyReminderTime = new(9, 0, 0);

    private TaskItemEditor(TaskItemDto item, Translations translations)
    {
        _item = item;
        _translations = translations;
    }

    /// <param name="events">
    /// What the calendar knows about, which the caller reads: the editor is handed the choices rather
    /// than reaching for a store, the same way it is handed the notification channels.
    /// </param>
    /// <inheritdoc cref="Inventory.InventoryItemEditor.Suggestions"/>
    public NameSuggestions? Suggestions { get; private init; }

    /// <param name="entriesOnTheList">
    /// Everything else on the list this entry is on, which is what it can be made to wait for. Handed in
    /// like the lists above it: which entries are on the list is the screen's knowledge. Left empty by a
    /// caller with nothing to offer, and the picker is then not drawn at all.
    /// </param>
    public static TaskItemEditor For(
        TaskItemDto item, Translations translations, CalendarEventDetailsDto? linkedEvent,
        IReadOnlyList<TaskListChoice> lists, NameSuggestions? suggestions = null,
        TaskItemShelfProduct? shelf = null, Func<TaskItemShelfProduct?>? shelfForSomethingNew = null,
        IReadOnlyList<TaskItemDto>? entriesOnTheList = null)
    {
        var editor = Build(
            item, translations, linkedEvent, lists, suggestions, shelf, shelfForSomethingNew,
            entriesOnTheList ?? []);
        if (suggestions is not null)
        {
            suggestions.Offers(NameSuggestionKind.TaskItemDescription);
            suggestions.StartsAt(editor.Description);
            suggestions.Takes = description => editor.Description = description;
        }

        return editor;
    }

    private static TaskItemEditor Build(
        TaskItemDto item, Translations translations, CalendarEventDetailsDto? linkedEvent,
        IReadOnlyList<TaskListChoice> lists, NameSuggestions? suggestions, TaskItemShelfProduct? shelf,
        Func<TaskItemShelfProduct?>? shelfForSomethingNew, IReadOnlyList<TaskItemDto> entriesOnTheList)
    {
        var editor = new TaskItemEditor(item, translations)
        {
            ShelfForSomethingNew = shelfForSomethingNew,
            Suggestions = suggestions,
            Channels = NotificationChannelChoice.All(translations),
            Kinds = TaskItemKindChoice.All(translations),
            Event = TaskItemEventForm.For(linkedEvent, translations),
            Shelf = shelf,
            // What the entry asks for when no shelf holds it. Not for one that already names a row:
            // that row is the answer, and the server keeps no product beside it.
            ProductWanted = item.LinkedInventoryItemId is null
                ? InventoryItemEditor.ForSomethingAListWillAskFor(item.Product, translations)
                : null,
            LinkedCalendarEventId = item.LinkedCalendarEventId,
            LinkableTaskLists = lists,
            // Everything but itself, and nothing that has never been saved: a step is named by id, and
            // an entry with none cannot be named. The browser's own picker draws the same two lines.
            WaitableEntries =
            [
                .. entriesOnTheList
                    .Where(candidate => candidate.Id != Guid.Empty && candidate.Id != item.Id)
                    .Select(candidate => TaskEntryChoice.For(candidate.Id, candidate.Description))
            ],
            Kind = item.Kind,
            // From the appointment when there is one, because that is where the place lives once the two
            // are linked - and from the entry when there is not, which is how an unlinked one holds it.
            Location = linkedEvent?.Location?.Address is { Length: > 0 } placed ? placed : item.Location,
            LocationLatitude = linkedEvent?.Location?.Latitude,
            LocationLongitude = linkedEvent?.Location?.Longitude,
            Description = item.Description,
            // An appointment made before the entry had a description of its own carries the answer on
            // the event. The one box opens showing it rather than blank, or the next save would write
            // the blank back over it - the same line Orbit.Web's editor draws.
            Notes = item.AllNotes.Length > 0 ? item.AllNotes : linkedEvent?.Description ?? string.Empty,
            Categories = CategoryText.Join(item.AllCategories),
            HasDueDate = item.DueDateUtc is not null,
            DueDate = item.DueDateUtc?.LocalDateTime.Date ?? DateTime.Today,
            OverdueNotificationChannel = item.OverdueNotificationChannel,
            RemindDaily = item.RemindDaily,
            DailyReminderNotificationChannel = item.DailyReminderNotificationChannel,
            // Midnight reads as "never set". The wire carries a plain TimeOnly and so cannot say
            // "none"; an entry reminded daily at exactly 00:00 is far more likely to be one nobody
            // chose an hour for than one somebody wanted at midnight - and being asked is a smaller
            // cost than a reminder arriving while everybody is asleep. Orbit.Web reads it the same way.
            HasDailyReminderTime = !item.RemindDaily || item.DailyReminderTimeOfDay != default,
            DailyReminderTime = item.DailyReminderTimeOfDay == default
                ? DefaultReminderTime
                : item.DailyReminderTimeOfDay.ToTimeSpan()
        };

        // What it already stands for, in the order the entry names them. Set after the initialiser
        // because the collection is the editor's own rather than something assigned to it.
        foreach (var linked in item.AllLinkedTaskListIds
            .Select(id => lists.FirstOrDefault(choice => choice.ServerId == id))
            .OfType<TaskListChoice>())
        {
            editor.LinkedTaskLists.Add(linked);
        }

        // And what it already waits for, in the order the entry names them. A step naming an entry that
        // is no longer on the list is dropped rather than drawn as a blank - which is what the server
        // does with it on the next save anyway. See TaskListSteps.
        foreach (var step in item.AllWaitsForTaskItemIds
            .Select(stepId => editor.WaitableEntries.FirstOrDefault(choice => choice.Id == stepId))
            .OfType<TaskEntryChoice>())
        {
            editor.WaitsFor.Add(step);
        }

        // The Save button answers to the appointment as well as to the entry - see TaskItemEventForm.
        editor.Event.Missing = editor.SayWhatTheFormShows;
        return editor;
    }

    public bool CanSave => Description.Trim().Length > 0 && WhatIsMissing is null;

    /// <summary>
    /// What stops this entry being saved, or null when nothing does. Refused rather than quietly
    /// corrected: a daily reminder with no hour would be sent at midnight, and an hour nobody chose is
    /// worse than being asked for one.
    /// </summary>
    public string? WhatIsMissing
        => RemindDaily && !HasDailyReminderTime
            ? _translations["A daily reminder needs a time to arrive at."]
            : IsCalendarEntry ? Event.WhatIsMissing
            : IsShelfEntry && !Shelf!.Product.CanSave
                // A product being described has no name box to fill in - only the amount can be missing.
                ? _translations[IsDescribingSomethingNew
                    ? "This errand's product needs an amount."
                    : "This errand's product needs a name and an amount."]
                : null;

    public bool HasSomethingMissing => WhatIsMissing is not null;

    /// <summary>
    /// Whether an hour has actually been chosen. False for an entry that arrived reminded daily at
    /// midnight, which is what "nobody chose one" looks like on the wire - see the factory.
    /// </summary>
    [ObservableProperty]
    private bool _hasDailyReminderTime = true;

    /// <summary>Nine in the morning, which is what the picker opens on when no hour has been chosen.</summary>
    private static readonly TimeSpan DefaultReminderTime = new(9, 0, 0);

    /// <summary>
    /// Everything this screen does not show - the id, whether it is done - travels through untouched.
    /// An entry linked to an inventory item's restock task must come back linked.
    /// </summary>
    public TaskItemDto ToDto()
        => _item with
        {
            // This screen offers one list where an entry may stand for several. Leaving the picker
            // alone therefore keeps every one of them - the phone must not throw away what it cannot
            // show. Actually choosing a different list is taken at its word: the entry then stands for
            // that one and no others.
            // The new field only: the old single one carries just the first list, so a save from this
            // phone would quietly drop the rest of an entry standing for several.
            LinkedTaskListId = null,
            LinkedTaskListIds = [.. LinkedTaskLists.Select(linked => linked.ServerId!.Value)],
            Description = Description.Trim(),
            // Always a string, never null, now that there is a box: an empty one means "cleared", which
            // is what emptying it has to mean - null would leave whatever the server holds.
            Notes = Notes.Trim(),
            Categories = CategoryText.Split(Categories),
            // What this entry asks for, where nothing on a shelf answers it yet. Null for every other
            // case, which is what tells the server to leave a stored one alone - an entry of another
            // kind must not empty a description by being saved beside it, and one that names a shelf
            // row has that row as its answer. Orbit.Web's ProductAsked draws the same two lines, and
            // its categories rule too: the entry's own box is what the product is filed under.
            Product = IsAskingForSomethingNoShelfHasYet
                ? ProductWanted!.ToTaskItemProduct() with { Categories = CategoryText.Split(Categories) }
                : null,
            // Converted rather than sent with the local offset the picker works in: Npgsql refuses a
            // DateTimeOffset with a non-zero offset for a "timestamp with time zone" column outright,
            // so a due date set here answered 500 and the queued save was given up on after five
            // tries - see CalendarViewModel, which has always converted for the same reason.
            DueDateUtc = HasDueDate
                ? new DateTimeOffset(DueDate.Date, TimeZoneInfo.Local.GetUtcOffset(DueDate.Date)).ToUniversalTime()
                : null,
            OverdueNotificationChannel = OverdueNotificationChannel,
            RemindDaily = RemindDaily,
            DailyReminderNotificationChannel = DailyReminderNotificationChannel,
            DailyReminderTimeOfDay = TimeOnly.FromTimeSpan(DailyReminderTime),
            Kind = Kind,
            Location = Location.Trim(),
            // Only a calendar entry keeps its appointment; anything else sends none, whatever it was
            // before. The same rule Orbit.Web's ToLinkedCalendarEventId applies. The id itself is
            // filled in by the screen when it puts the appointment in the calendar - see
            // TaskListDetailViewModel.PutAppointmentsInTheCalendarAsync.
            LinkedCalendarEventId = IsCalendarEntry ? LinkedCalendarEventId : null,
            // The order the work has to be done in, as this form now says it. Read off the picker rather
            // than passed through: the phone can show every step there is, because a step is always an
            // entry of the list this form was opened from. A step whose entry has gone is already absent
            // from WaitsFor - see Build - and the server drops such an id anyway (TaskListSteps).
            WaitsForTaskItemIds = [.. WaitsFor.Select(step => step.Id)]
        };

    partial void OnDescriptionChanged(string value)
    {
        OnPropertyChanged(nameof(CanSave));
        Suggestions?.ShowFor(value);
    }

    /// <summary>Choosing an hour is what answers the refusal above, so the picker records that it was.</summary>
    partial void OnDailyReminderTimeChanged(TimeSpan value)
    {
        HasDailyReminderTime = true;
        SayWhetherItCanBeSaved();
    }

    /// <summary>
    /// Puts an hour on an entry that arrived without one, and opens the picker at it. Its own button
    /// rather than a picker showing nine o'clock from the start: a picker already showing an hour is
    /// one somebody can accept by not touching it, and accepting it would leave nothing recorded - the
    /// refusal would stand with no way through it. The browser has no such trap, its field being empty.
    /// </summary>
    [RelayCommand]
    private void ChooseAReminderTime()
    {
        DailyReminderTime = DefaultReminderTime;
        HasDailyReminderTime = true;
    }

    partial void OnRemindDailyChanged(bool value) => SayWhetherItCanBeSaved();

    partial void OnHasDailyReminderTimeChanged(bool value) => SayWhetherItCanBeSaved();

    private void SayWhetherItCanBeSaved()
    {
        OnPropertyChanged(nameof(WhatIsMissing));
        OnPropertyChanged(nameof(HasSomethingMissing));
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnKindChanged(string value)
    {
        // An entry becomes an errand, or stops being one. A product already on a shelf is left alone -
        // the entry still names it, and changing the kind back and forth must not lose the amounts
        // somebody typed - but the form for a new one appears and disappears with the choice.
        if (value == nameof(TaskItemKind.Inventory))
        {
            Shelf ??= ShelfForSomethingNew?.Invoke();
        }
        else if (Shelf is { Product.IsSomethingNew: true })
        {
            Shelf = null;
        }

        OnPropertyChanged(nameof(Shelf));
        OnPropertyChanged(nameof(IsDescribingSomethingNew));
        SayWhatTheFormShows();
    }

    /// <summary>
    /// What appears and disappears together: the appointment's form and the place it happens are shown
    /// for a Calendar entry and hidden for the rest, and whether it can be saved depends on both halves.
    /// </summary>
    private void SayWhatTheFormShows()
    {
        OnPropertyChanged(nameof(IsCalendarEntry));
        OnPropertyChanged(nameof(IsPlaceEntry));
        OnPropertyChanged(nameof(CanSayWhereItHappens));
        OnPropertyChanged(nameof(IsShelfEntry));
        OnPropertyChanged(nameof(IsAskingForSomethingNoShelfHasYet));
        OnPropertyChanged(nameof(HasNoProductToEdit));
        OnPropertyChanged(nameof(WhereTheProductLives));
        OnPropertyChanged(nameof(WhatIsMissing));
        OnPropertyChanged(nameof(HasSomethingMissing));
        OnPropertyChanged(nameof(CanSave));
    }
}
