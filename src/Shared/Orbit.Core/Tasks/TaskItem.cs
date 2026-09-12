using Orbit.Core.Abstractions;
using Orbit.Core;
using Orbit.Core.Notifications;

namespace Orbit.Core.Tasks;

/// <summary>
/// A single checklist entry within a <see cref="TaskList"/>, with its own due date and completion
/// state - or, if <see cref="LinkedTaskListIds"/> holds anything, a reference to other task lists of
/// the same user instead of an independently completable entry (see
/// <see cref="LinkedTaskCompletionResolver"/> for how its completion is derived, and
/// <see cref="TaskListLinkValidator"/> for how the links themselves are validated).
/// </summary>
public sealed class TaskItem
{
    public Guid Id { get; private set; }
    public string Description { get; private set; }

    /// <summary>
    /// The longer text about this entry - what it is really for, where the line above is what it is
    /// called. Empty for an entry nobody has written one on, which is most of them.
    ///
    /// Called Notes rather than Description only because <see cref="Description"/> is already taken by
    /// the entry's own line, which is really its title: a calendar entry's words are its event's title,
    /// and a checklist line is the thing to do. On screen this is "Description", the same word the
    /// calendar has always used for it - and for a calendar entry it *is* the event's description, so
    /// there is one box rather than two copies drifting apart (see TaskEditor.razor).
    /// </summary>
    public string Notes { get; private set; }
    public DateTimeOffset? DueDateUtc { get; private set; }
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Whether this entry was closed <b>without</b> being done - the cross beside the tick. It is not a
    /// third kind of "still to do": a failed entry is finished with, so it stops being owed exactly the
    /// way a completed one does (see <see cref="IsResolved"/>, which is the question everything asking
    /// "is this still on somebody's plate" asks). What it is not is <em>done</em>, so it never counts
    /// towards how much of a list is finished, and nothing that acts on work having been done - a shelf
    /// topped up by a restock errand, most of all - acts on it.
    ///
    /// Never true at the same time as <see cref="IsCompleted"/>: the two are one answer with three
    /// values, kept as two flags because everything that already asked "is this ticked" still means the
    /// same thing by it. The constructor is where that is enforced.
    /// </summary>
    public bool IsFailed { get; private set; }

    /// <summary>
    /// Whether this entry is finished with, either way - ticked off or given up on. The question every
    /// count of what is still owed asks: reminders, a list's own completion, what is due today.
    /// </summary>
    public bool IsResolved => IsCompleted || IsFailed;

    /// <summary>
    /// The other task lists this entry stands for, in the order somebody put them in. Empty for an
    /// ordinary entry, which is the usual case.
    ///
    /// Several rather than one because a step is often more than one list - "the flat is ready" means
    /// the kitchen and the bathroom and the hall - and writing that as three entries saying the same
    /// thing loses that they are one step. It is done when every list it names is done: any other rule
    /// would let the entry read as finished while work it stands for is still outstanding.
    /// </summary>
    public IReadOnlyList<Guid> LinkedTaskListIds { get; private set; }

    /// <summary>
    /// Whether this entry is a pointer at other lists rather than work of its own. What separates the
    /// two everywhere: a link is not counted as work, not ticked by hand, and not reopened.
    /// </summary>
    public bool IsALinkToOtherLists => LinkedTaskListIds.Count > 0;

    /// <summary>
    /// The entries <b>on this same list</b> that have to be done before this one can be - "hang the
    /// door" after "fit the hinges". Empty for an ordinary entry, which is nearly all of them.
    ///
    /// Only ever entries of its own list, which is what makes it a different thing from
    /// <see cref="LinkedTaskListIds"/>: that is one entry standing for whole other lists, this is the
    /// order the work on one list has to be done in. Kept as ids in the order they were chosen, and
    /// what they mean is enforced where a list is built - see <see cref="TaskListSteps"/>, which drops
    /// an id that is not an entry here and refuses the tick while any of them is unfinished.
    /// </summary>
    public IReadOnlyList<Guid> WaitsForTaskItemIds { get; private set; }

    /// <summary>Whether anything has to happen before this entry can be crossed off.</summary>
    public bool WaitsForAnything => WaitsForTaskItemIds.Count > 0;

    /// <summary>
    /// The ways this entry can be got done, any one of which is enough - "the sauce": buy a ready one,
    /// or make it from a list of its own. Empty for an ordinary entry, which is nearly all of them.
    ///
    /// The other half of <see cref="LinkedTaskListIds"/>, and kept apart from it on purpose: that is one
    /// step that is every one of its lists, this is one step that is any one of its ways. And a way can
    /// be a line of its own, so an alternative that is a single errand needs no list made for it. An
    /// entry has one or the other, never both - see the constructor.
    ///
    /// While it has any, the entry's tick is theirs: it is done exactly when one of them is. A way that
    /// is a list is done when that list is, worked out on every read - see
    /// <see cref="LinkedTaskCompletionResolver"/>.
    /// </summary>
    public IReadOnlyList<TaskItemAlternative> Alternatives { get; private set; }

    /// <summary>Whether this entry is done any one of several ways rather than by a tick of its own.</summary>
    public bool HasAlternatives => Alternatives.Count > 0;

    /// <summary>
    /// Every other list this entry points at, as a list it stands for or as a way of doing it - what
    /// <see cref="TaskListLinkValidator"/> checks, since a way can close a loop as surely as a link can.
    /// </summary>
    public IEnumerable<Guid> TaskListIdsItPointsAt
        => LinkedTaskListIds
            .Concat(Alternatives.Where(way => way.IsAList).Select(way => way.LinkedTaskListId!.Value))
            .Distinct();

    /// <summary>
    /// When this entry was first stored - what decides which member of a reference group takes over when
    /// the entry the others point at is deleted (see <see cref="TaskItemReferences"/>). Kept by id across
    /// every save, since a save replaces the rows wholesale. Null for an entry stored before it was
    /// recorded, which then counts as the newest.
    /// </summary>
    public DateTimeOffset? CreatedAtUtc { get; private set; }

    /// <summary>
    /// The entry this one is the same thing as, when it is one - picked from the name suggestions, so
    /// "Sauce" on the burger list and "Sauce" on the pasta list are one object rather than two. The entry
    /// pointed at is the group's source. Every member keeps the shared details itself, and a save of any
    /// member passes them on to the rest - see <see cref="TaskItemReferences"/>. Always the source, never a
    /// member that points further on. Null for an entry of its own.
    /// </summary>
    public Guid? ReferencesTaskItemId { get; private set; }

    /// <summary>
    /// How much of its product this entry needs - its own minimum, and the one product detail a reference
    /// group does not share: two recipes asking for the same sauce may need different amounts. Kept when
    /// the entry comes to stand for a shelf item and <see cref="Product"/> is dropped; a shelf item's
    /// minimum can never fall below these added up (see Orbit.Core.Inventories.InventoryItem.Usage). Null
    /// reads as one, the counting rule's answer for a line that says nothing.
    /// </summary>
    public decimal? RequiredQuantity { get; private set; }

    /// <summary>What this entry is and what it stands for - see <see cref="TaskItemSubject"/>.</summary>
    public TaskItemSubject Subject { get; private set; }

    // The four answers above, each still readable on its own, for the same reason the reminders are -
    // everything that acts on them asks one question at a time.

    /// <inheritdoc cref="TaskItemSubject.Kind"/>
    public TaskItemKind Kind => Subject.Kind;

    /// <inheritdoc cref="TaskItemSubject.Location"/>
    public string Location => Subject.Location;

    /// <summary>
    /// The calendar event this entry is the same appointment as, when it is one. The event is where the
    /// place and the time then live, so the two cannot drift apart.
    ///
    /// Not validated: an event that is later deleted leaves this pointing at nothing, and a client
    /// reading it treats that as "no event" - the same way a link to a deleted task list is treated as
    /// "not completed" rather than as a failure (see LinkedTaskCompletionResolver).
    /// </summary>
    public Guid? LinkedCalendarEventId => Subject.LinkedCalendarEventId;

    /// <summary>
    /// The shelf item this entry is an errand about, when it is one - see
    /// <see cref="TaskItemKind.Inventory"/>. Null for every other kind.
    ///
    /// Not validated, for the same reason <see cref="LinkedCalendarEventId"/> is not: an item deleted
    /// afterwards leaves this pointing at nothing, and a reader treats that as "no shelf item" rather
    /// than as a failure.
    /// </summary>
    public Guid? LinkedInventoryItemId => Subject.LinkedInventoryItemId;

    /// <summary>
    /// What this entry is about, in the reader's own words - "shopping", "car", "the flat". Free text
    /// rather than a fixed list, the way a shelf item's category is (see InventoryItem.Category), but
    /// several of them: one errand is often two subjects at once, and being made to pick the single
    /// truest one is how a category stops being written at all.
    ///
    /// Kept for every kind of entry, checklist and appointment alike - what something is about does not
    /// depend on whether it also has a time.
    /// </summary>
    public IReadOnlyList<string> Categories { get; private set; }

    /// <summary>
    /// What this entry asks for, in the detail a shelf item is kept in - see
    /// <see cref="TaskItemProduct"/>. Null for every kind but <see cref="TaskItemKind.Inventory"/>, and
    /// null for an inventory entry that already stands for a real shelf item: that item is then the
    /// answer, and a second copy here is the one that would go stale.
    /// </summary>
    public TaskItemProduct? Product { get; private set; }

    /// <summary>
    /// How much this entry matters. The list it is on has one of its own and this is not it: a list of
    /// ten errands usually has one that has to happen and nine that can wait, and until now the only way
    /// to say so was to split the list in two.
    /// </summary>
    public ItemPriority Priority { get; private set; }

    /// <summary>
    /// What colour this entry is drawn in, as the reader chose it - the same shape a calendar event's
    /// colour takes (see CalendarEventDetails.Color), and empty for an entry nobody chose one for, which
    /// every screen reads as "whatever this kind is drawn in".
    /// </summary>
    public string Colour { get; private set; }

    /// <summary>When this entry speaks up and where - see <see cref="TaskItemReminders"/>.</summary>
    public TaskItemReminders Reminders { get; private set; }

    // The four settings above, each still readable on its own. Everything that acts on them - the two
    // schedulers, the repository, the endpoints - asks one question at a time, and making forty read
    // sites say "Reminders." would be a wider change than the one this grouping is for, which is the
    // pile of parameters every way of making an entry had to carry.

    /// <inheritdoc cref="TaskItemReminders.WhenOverdue"/>
    public NotificationChannel OverdueNotificationChannel => Reminders.WhenOverdue;

    /// <inheritdoc cref="TaskItemReminders.Daily"/>
    public bool RemindDaily => Reminders.Daily;

    /// <inheritdoc cref="TaskItemReminders.DailyChannel"/>
    public NotificationChannel DailyReminderNotificationChannel => Reminders.DailyChannel;

    /// <inheritdoc cref="TaskItemReminders.DailyTimeOfDay"/>
    public TimeOnly DailyReminderTimeOfDay => Reminders.DailyTimeOfDay;

    private TaskItem(
        Guid id, string description, DateTimeOffset? dueDateUtc, bool isCompleted, IReadOnlyList<Guid>? linkedTaskListIds,
        TaskItemReminders? reminders, TaskItemSubject? subject, IReadOnlyList<string>? categories,
        TaskItemProduct? product, string? notes, bool isFailed = false,
        IReadOnlyList<Guid>? waitsForTaskItemIds = null,
        ItemPriority priority = ItemPriority.Normal, string? colour = null,
        IReadOnlyList<TaskItemAlternative>? alternatives = null,
        DateTimeOffset? createdAtUtc = null, Guid? referencesTaskItemId = null, decimal? requiredQuantity = null)
    {
        Id = id;
        Description = description;
        Notes = notes ?? string.Empty;
        Priority = priority;
        Colour = (colour ?? string.Empty).Trim();
        DueDateUtc = dueDateUtc;
        // Distinct and in order: naming the same list twice is one link written twice, not two steps,
        // and it would make the entry look like it stands for more work than it does.
        LinkedTaskListIds = linkedTaskListIds is null ? [] : [.. linkedTaskListIds.Distinct()];
        // One or the other: an entry standing for lists is done when all of them are and one with ways
        // when any is, and nothing sensible is meant by both at once. The links were here first and
        // win; the ways are dropped rather than refused, the way a product on the wrong kind of entry
        // is. A way with neither words nor a list is nothing to choose between, and goes too.
        Alternatives = LinkedTaskListIds.Count > 0 || alternatives is null
            ? []
            : [.. alternatives
                .Select(way => way with { Description = (way.Description ?? string.Empty).Trim() })
                .Where(way => way.Description.Length > 0 || way.IsAList)];
        // While it has ways, its tick is theirs.
        IsCompleted = Alternatives.Count > 0 ? Alternatives.Any(way => way.IsDone) : isCompleted;
        // Three states out of two flags, settled in the one place every entry is built: a tick wins over
        // a cross, so nothing downstream has to decide what an entry claiming both would mean. A linked
        // entry has neither of its own - its completion follows the lists it stands for.
        IsFailed = isFailed && !IsCompleted;
        // The same rule, and one more: an entry cannot wait for itself, which is a step that could
        // never be taken rather than an ordering anybody meant.
        WaitsForTaskItemIds = waitsForTaskItemIds is null
            ? []
            : [.. waitsForTaskItemIds.Distinct().Where(waitedFor => waitedFor != id)];
        Reminders = reminders ?? TaskItemReminders.Default;
        Subject = subject ?? TaskItemSubject.PlainWork;
        Categories = TidyCategories(categories);
        // The same rule the subject applies to its own links, and for the same reason: a description of
        // something to put on a shelf means nothing on an appointment, and nothing on an entry that
        // already points at the shelf item itself. Dropped rather than refused, so changing an entry's
        // kind loses what no longer applies instead of failing the save.
        Product = Subject.Kind == TaskItemKind.Inventory && Subject.LinkedInventoryItemId is null
            // Filed the way the entry's own categories are filed, and for the same reason: a word
            // written twice is one category, and a filter comparing them without case would otherwise
            // show two chips meaning the same thing.
            ? product is null ? null : product with { Categories = TidyCategories(product.Categories) }
            : null;
        CreatedAtUtc = createdAtUtc;
        // Never itself: an entry pointing at its own id is an entry of its own.
        ReferencesTaskItemId = referencesTaskItemId == id ? null : referencesTaskItemId;
        // The entry's own minimum, which the product it describes also says while there is one - one
        // answer in two places, so the explicit one wins and the product is told.
        RequiredQuantity = requiredQuantity ?? product?.MinimumQuantity;
        if (Product is not null && Product.MinimumQuantity != RequiredQuantity)
        {
            Product = Product with { MinimumQuantity = RequiredQuantity };
        }
    }

    /// <summary>
    /// What is worth storing of what was typed: blanks dropped, edges trimmed, and the same word said
    /// twice kept once - written in the order they were given, because that is the order the reader
    /// thinks of them in. "Shopping" and "shopping" are one category: a filter that told them apart
    /// would quietly hide half of what it was asked for.
    /// </summary>
    private static IReadOnlyList<string> TidyCategories(IReadOnlyList<string>? categories)
        => categories is null
            ? []
            : [.. categories
                .Select(category => category.Trim())
                .Where(category => category.Length > 0)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>
    /// Brings a finished entry back as something still to do, keeping its identity - the same row the
    /// reader already knows, rather than a second one beside it. Used where a task is meant to recur:
    /// an inventory item that is low again, and a daily reminder coming round.
    ///
    /// A linked entry is left alone: its completion follows the list it links to, and forcing it here
    /// would be overwritten by the next resolve anyway.
    /// </summary>
    public void Reopen()
    {
        if (IsALinkToOtherLists)
        {
            return;
        }

        // An entry done one of several ways comes back with none of them taken: the ways stay, since
        // they are what the entry is, and the choice is made again.
        Alternatives = [.. Alternatives.Select(way => way with { IsDone = false })];
        IsCompleted = false;
        // A cross is a way of being finished with something, so bringing the entry back as work
        // clears it too - a reopened entry nobody has answered yet is neither done nor given up on.
        IsFailed = false;
    }

    /// <summary>
    /// Crosses an entry off. Used where something other than the reader's own tick establishes that the
    /// work is done - an inventory that turns out to hold what the entry asks for.
    ///
    /// A linked entry is left alone for the same reason <see cref="Reopen"/> leaves it: its completion
    /// follows the list it points at. So is one done by ways, since nothing here says which way it was.
    /// </summary>
    public void Complete()
    {
        if (!IsALinkToOtherLists && !HasAlternatives)
        {
            IsCompleted = true;
            IsFailed = false;
        }
    }

    /// <summary>
    /// Keeps the ways this entry is already done by, for a caller that said nothing about them - the
    /// sixth field to follow this rule (see UpdateTaskListCommand.EntriesKeepingTheirAlternatives). The
    /// tick follows them back: a client that knows nothing of ways cannot have meant its own tick on an
    /// entry whose tick is theirs.
    /// </summary>
    public void KeepAlternativesOf(TaskItem stored)
    {
        if (IsALinkToOtherLists)
        {
            return;
        }

        Alternatives = stored.Alternatives;
        if (HasAlternatives)
        {
            IsCompleted = Alternatives.Any(way => way.IsDone);
            IsFailed = IsFailed && !IsCompleted;
        }
    }

    /// <summary>
    /// Keeps what this entry is the same thing as, and how much of it it needs, for a caller that said
    /// nothing about either - the seventh field to follow this rule (see
    /// UpdateTaskListCommand.EntriesKeepingTheirReference). A minimum the product it describes still
    /// carries is taken at its word, since that is the one an older client can say.
    /// </summary>
    public void KeepReferenceOf(TaskItem stored)
    {
        ReferencesTaskItemId = stored.ReferencesTaskItemId;
        RequiredQuantity ??= stored.RequiredQuantity;
        if (Product is not null)
        {
            Product = Product with { MinimumQuantity = RequiredQuantity };
        }
    }

    /// <summary>When this entry was first stored - see <see cref="CreatedAtUtc"/> and TaskItemReferences.StampCreationTimes.</summary>
    internal void StampCreation(DateTimeOffset? createdAtUtc) => CreatedAtUtc = createdAtUtc;

    /// <summary>Points this entry at its group's source, or at nothing - see TaskItemReferences.</summary>
    internal void PointReferenceAt(Guid? sourceId) => ReferencesTaskItemId = sourceId == Id ? null : sourceId;

    /// <summary>
    /// Takes on the details a reference group shares from another member of it: what kind of entry it is
    /// and where, what it is filed under, what it says, how much it matters, what colour it is drawn in,
    /// and the shelf item or the product it asks for. Its own date, tick, ways, reminders, appointment and
    /// minimum stay its own. Answers whether anything changed, so a list nobody touched is not saved again.
    /// See TaskItemReferences.
    /// </summary>
    internal bool TakeSharedDetailsFrom(TaskItem other)
    {
        var subject = new TaskItemSubject(other.Kind, other.Location, LinkedCalendarEventId, other.LinkedInventoryItemId);
        var product = subject.Kind == TaskItemKind.Inventory && subject.LinkedInventoryItemId is null && other.Product is not null
            ? other.Product with { MinimumQuantity = RequiredQuantity }
            : null;
        var changed = subject != Subject
            || !Categories.SequenceEqual(other.Categories)
            || Notes != other.Notes
            || Priority != other.Priority
            || Colour != other.Colour
            || !Equals(product, Product);

        Subject = subject;
        Categories = other.Categories;
        Notes = other.Notes;
        Priority = other.Priority;
        Colour = other.Colour;
        Product = product;
        return changed;
    }

    /// <summary>
    /// Keeps what this entry is already filed under, for a caller that said nothing about it - see
    /// UpdateTaskListCommand.EntriesKeepingTheirCategories.
    /// </summary>
    public void KeepCategoriesOf(TaskItem stored) => Categories = stored.Categories;

    /// <summary>
    /// Keeps the steps this entry already waits for, for a caller that said nothing about them - the
    /// same rule the categories, the product and the description follow, and for the same reason: a
    /// client written before steps existed goes on saving lists without dropping the order somebody
    /// arranged on the web. See UpdateTaskListCommand.EntriesKeepingTheirSteps.
    /// </summary>
    public void KeepStepsOf(TaskItem stored) => WaitsForTaskItemIds = stored.WaitsForTaskItemIds;

    /// <summary>
    /// Keeps the description this entry already has, for a caller that said nothing about it - the same
    /// rule the categories and the product follow, and for the same reason: a client written before an
    /// entry could carry one goes on saving lists without wiping what was typed on the web. See
    /// UpdateTaskListCommand.EntriesKeepingTheirNotes.
    /// </summary>
    public void KeepNotesOf(TaskItem stored) => Notes = stored.Notes;

    /// <summary>
    /// The same for how an entry is drawn and how much it matters, when a request said nothing about
    /// either - see UpdateTaskListCommand.EntriesKeepingTheirLook. One method for the two because no
    /// client sends one without the other: a client either knows about them or does not.
    /// </summary>
    public void KeepLookOf(TaskItem stored)
    {
        Priority = stored.Priority;
        Colour = stored.Colour;
    }

    /// <summary>
    /// Points this entry at the shelf item it turned out to be about - what generating a storage from a
    /// list does to the entries it built that storage from (see
    /// GenerateInventoryFromTaskListCommandHandler). The description it carried is dropped in the same
    /// breath: the shelf item now answers everything the description was standing in for, and keeping
    /// both is keeping two answers that drift apart.
    /// </summary>
    public void PointAtShelfItem(Guid inventoryItemId)
    {
        Subject = new TaskItemSubject(TaskItemKind.Inventory, linkedInventoryItemId: inventoryItemId);
        Product = null;
    }

    /// <summary>
    /// Keeps what this entry already asks for, for a caller that said nothing about it - the same rule
    /// the categories follow, and for the same reason: a client written before an entry could describe a
    /// product goes on saving lists without emptying the description somebody wrote on the web. See
    /// UpdateTaskListCommand.EntriesKeepingTheirProduct.
    /// </summary>
    public void KeepProductOf(TaskItem stored)
        => Product = Kind == TaskItemKind.Inventory && LinkedInventoryItemId is null ? stored.Product : null;

    /// <summary>
    /// A linked item's completion can't be set directly - it always follows the lists it links to (see
    /// <see cref="LinkedTaskCompletionResolver"/>) - so <paramref name="isCompleted"/> is ignored in
    /// favor of "not completed" whenever <paramref name="linkedTaskListIds"/> holds anything.
    /// </summary>
    public static TaskItem Create(
        string description, DateTimeOffset? dueDateUtc, bool isCompleted, IReadOnlyList<Guid>? linkedTaskListIds = null,
        TaskItemReminders? reminders = null, TaskItemSubject? subject = null, IReadOnlyList<string>? categories = null,
        TaskItemProduct? product = null, string? notes = null, bool isFailed = false,
        IReadOnlyList<Guid>? waitsForTaskItemIds = null,
        ItemPriority priority = ItemPriority.Normal, string? colour = null,
        IReadOnlyList<TaskItemAlternative>? alternatives = null,
        Guid? referencesTaskItemId = null, decimal? requiredQuantity = null)
    {
        // Here rather than in the constructor, which FromPersistence also uses: a row already stored
        // fits by definition, and rejecting one on the way back out would make an old entry unreadable
        // rather than telling anybody anything.
        StoredTextLimits.OrRefuse(description, StoredTextLimits.TaskDescription, "task entry");
        // The same room a calendar event's description has, because on a calendar entry this is that
        // description - see the property.
        StoredTextLimits.OrRefuse(notes ?? string.Empty, StoredTextLimits.EventDescription, "task entry's description");
        // The place as the subject actually keeps it: an address too long to store is refused, and one
        // an entry of this kind does not keep at all was already dropped - see TaskItemSubject.
        StoredTextLimits.OrRefuse(subject?.Location ?? string.Empty, StoredTextLimits.Address, "place's address");
        foreach (var category in categories ?? [])
        {
            StoredTextLimits.OrRefuse(category, StoredTextLimits.Category, "task entry's category");
        }

        StoredTextLimits.OrRefuse(product?.ProductType ?? string.Empty, StoredTextLimits.ProductType, "product's type");
        foreach (var category in product?.Categories ?? [])
        {
            StoredTextLimits.OrRefuse(category, StoredTextLimits.Category, "product's category");
        }

        StoredTextLimits.OrRefuse(colour ?? string.Empty, StoredTextLimits.Color, "task entry's colour");
        foreach (var way in alternatives ?? [])
        {
            StoredTextLimits.OrRefuse(way.Description ?? string.Empty, StoredTextLimits.TaskDescription, "way of doing a task entry");
        }

        var standsOnItsOwn = linkedTaskListIds is null || linkedTaskListIds.Count == 0;
        // A way that is a list is done when that list is, whatever the client says - the same override
        // standsOnItsOwn applies to the entry's own tick.
        var ways = alternatives?.Select(way => way.IsAList ? way with { IsDone = false } : way).ToList();
        return new TaskItem(
            Guid.NewGuid(), description, dueDateUtc, standsOnItsOwn && isCompleted, linkedTaskListIds,
            reminders, subject, categories, product, notes, standsOnItsOwn && isFailed, waitsForTaskItemIds,
            priority, colour, ways, DateTimeOffset.UtcNow, referencesTaskItemId, requiredQuantity);
    }

    /// <summary>
    /// The same entry under a new name. Used when two clients hand over the same id and neither may keep
    /// it - see <see cref="TaskItemIdentity"/>. Everything else travels: what the entry is does not
    /// change, only what it is called.
    /// </summary>
    public TaskItem WithNewId()
        => new(
            Guid.NewGuid(), Description, DueDateUtc, IsCompleted, LinkedTaskListIds,
            Reminders, Subject, Categories, Product, Notes, IsFailed, WaitsForTaskItemIds, Priority, Colour,
            Alternatives, CreatedAtUtc, ReferencesTaskItemId, RequiredQuantity);

    /// <summary>
    /// This entry with its completion worked out from the lists it points at: every one of them for an
    /// entry standing for lists, and each way that is a list for an entry done by ways. Everything else
    /// it carries comes along. The resolver used to rebuild a linked entry from its id, words, date and
    /// reminders alone, so a read handed back a linked entry without its notes, kind, colour or priority.
    /// </summary>
    internal TaskItem ResolvedAgainst(Func<Guid, bool> isListDone)
        => new(
            Id, Description, DueDateUtc,
            IsALinkToOtherLists ? LinkedTaskListIds.All(isListDone) : IsCompleted,
            LinkedTaskListIds, Reminders, Subject, Categories, Product, Notes, IsFailed, WaitsForTaskItemIds,
            Priority, Colour,
            [.. Alternatives.Select(way => way.IsAList ? way with { IsDone = isListDone(way.LinkedTaskListId!.Value) } : way)],
            CreatedAtUtc, ReferencesTaskItemId, RequiredQuantity);

    /// <summary>
    /// Rebuilds a checklist entry from already-known values, bypassing the completion override above -
    /// used both to reload an entry as persisted, and by <see cref="LinkedTaskCompletionResolver"/> to
    /// apply a freshly resolved completion value to a linked entry.
    /// </summary>
    public static TaskItem FromPersistence(
        Guid id, string description, DateTimeOffset? dueDateUtc, bool isCompleted, IReadOnlyList<Guid>? linkedTaskListIds,
        TaskItemReminders? reminders, TaskItemSubject? subject = null, IReadOnlyList<string>? categories = null,
        TaskItemProduct? product = null, string? notes = null, bool isFailed = false,
        IReadOnlyList<Guid>? waitsForTaskItemIds = null,
        ItemPriority priority = ItemPriority.Normal, string? colour = null,
        IReadOnlyList<TaskItemAlternative>? alternatives = null,
        DateTimeOffset? createdAtUtc = null, Guid? referencesTaskItemId = null, decimal? requiredQuantity = null)
        => new(
            id, description, dueDateUtc, isCompleted, linkedTaskListIds, reminders, subject, categories, product,
            notes, isFailed, waitsForTaskItemIds, priority, colour, alternatives,
            createdAtUtc, referencesTaskItemId, requiredQuantity);

    /// <summary>
    /// Takes the tick back off an entry that may not carry one yet, because something it waits for is
    /// unfinished - see TaskListSteps, which is the only caller and where the rule itself lives.
    /// </summary>
    internal void CannotBeDoneYet() => IsCompleted = false;

    /// <summary>
    /// Keeps only the steps that are entries on this list, dropping an id that names nothing here - a
    /// step deleted since, or a client naming an entry of another list. See TaskListSteps.
    /// </summary>
    internal void WaitsOnlyFor(IReadOnlySet<Guid> idsOnThisList)
        => WaitsForTaskItemIds = [.. WaitsForTaskItemIds.Where(idsOnThisList.Contains)];
}
