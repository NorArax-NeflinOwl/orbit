using System.Net;
using System.Net.Http.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Notes;
using Orbit.Core.Folders;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Orbit.Web.Tests;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// Covers the notes overview: what it lists, what it shows of each note, and what deleting one does -
/// including the confirmation that stands in front of it.
/// </summary>
public sealed class NotesTests : OrbitTestContext
{
    private readonly List<HttpRequestMessage> _requests = [];

    public NotesTests() => Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    /// <summary>
    /// "/notes" is the newest note, open for writing in, with the column of every note beside it
    /// (2026-09-20, asked for). A note app is read by writing in one, and the page of cards in between
    /// was a press spent choosing which note to want.
    /// </summary>
    [Fact]
    public void Opening_the_notes_goes_straight_into_the_newest_one()
    {
        var older = Note("Ideas") with { UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1) };
        var newest = Note("Shopping") with { UpdatedAtUtc = DateTimeOffset.UtcNow };
        RegisterNotesApiClient([older, newest]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo("/notes");

        RenderComponent<Web.Pages.Notes>();

        Assert.EndsWith($"/notes/{newest.Id}", navigationManager.Uri, StringComparison.Ordinal);
    }

    /// <summary>
    /// Not into a sealed one, and not into one put away. A private note says nothing until its key is
    /// given, so landing on one opens a page that cannot be read; the archive is where things go to
    /// stop being what somebody is doing now.
    /// </summary>
    [Fact]
    public void It_does_not_open_a_sealed_note_or_one_put_away()
    {
        var plain = Note("Ideas") with { UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1) };
        RegisterNotesApiClient([
            plain,
            Note("Diary") with { IsPrivate = true, UpdatedAtUtc = DateTimeOffset.UtcNow },
            Note("Last year") with { IsArchived = true, UpdatedAtUtc = DateTimeOffset.UtcNow }]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo("/notes");

        RenderComponent<Web.Pages.Notes>();

        Assert.EndsWith($"/notes/{plain.Id}", navigationManager.Uri, StringComparison.Ordinal);
    }

    /// <summary>And the page of cards, under its own address, stays where it is.</summary>
    [Fact]
    public void The_page_of_cards_keeps_its_own_address()
    {
        RegisterNotesApiClient([Note("Shopping")]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo("/notes/all");

        RenderComponent<Web.Pages.Notes>();

        Assert.EndsWith("/notes/all", navigationManager.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_note_is_listed()
    {
        RegisterNotesApiClient([Note("Shopping"), Note("Ideas")]);

        var cut = RenderComponent<Web.Pages.Notes>();

        var markup = cut.Markup;
        Assert.Contains("Shopping", markup);
        Assert.Contains("Ideas", markup);
    }

    /// <summary>
    /// The page narrows by the reader's own words for their notes, in the browser the tasks page
    /// narrows by its categories in - see ValueBrowser. Each word says how many notes carry it, which
    /// is what makes one worth ticking.
    /// </summary>
    [Fact]
    public void Choosing_a_tag_leaves_only_the_notes_carrying_it()
    {
        RegisterNotesApiClient([
            Note("Shopping") with { Tags = ["home"] },
            Note("Ideas") with { Tags = ["work"] }]);
        var cut = RenderComponent<Web.Pages.Notes>();

        cut.Find(".value-browser-field").Click();
        var rows = cut.FindAll(".value-browser-row").ToList();
        Assert.Equal(["home", "work"], rows.Select(row => row.QuerySelector(".value-browser-name")!.TextContent.Trim()));
        Assert.Equal(["1", "1"], rows.Select(row => row.QuerySelector(".value-browser-count")!.TextContent.Trim()));

        rows[0].QuerySelector("input[type=checkbox]")!.Change(true);

        Assert.Equal(["Shopping"], cut.FindAll(".item-card-name").Select(card => card.TextContent.Trim()));
    }

    /// <summary>
    /// Two ticked words mean either of them, which is what choosing a second one usually means - the
    /// same reading the tasks page's categories take by default.
    /// </summary>
    [Fact]
    public void Two_tags_mean_either_of_them()
    {
        RegisterNotesApiClient([
            Note("Shopping") with { Tags = ["home"] },
            Note("Ideas") with { Tags = ["work"] },
            Note("Poem")]);
        var cut = RenderComponent<Web.Pages.Notes>();

        cut.Find(".value-browser-field").Click();
        // Found again between the two ticks: the first re-renders the page, and the second row's
        // handler belongs to the render before it.
        for (var index = 0; index < 2; index++)
        {
            cut.FindAll(".value-browser-row input[type=checkbox]").ToList()[index].Change(true);
        }

        Assert.Equal(["Shopping", "Ideas"], cut.FindAll(".item-card-name").Select(card => card.TextContent.Trim()));
    }

    /// <summary>
    /// And when the tags narrow everything away the page says which of the two did it: a folder is left
    /// by pressing another tab, a tag by unticking it.
    /// </summary>
    [Fact]
    public void Tags_that_match_nothing_say_so_rather_than_blaming_the_folder()
    {
        RegisterNotesApiClient([
            Note("Shopping") with { Tags = ["home"] },
            Note("Diary") with { IsPrivate = true }]);
        var cut = RenderComponent<Web.Pages.Notes>();

        cut.Find(".value-browser-field").Click();
        cut.Find(".value-browser-row input[type=checkbox]").Change(true);
        // A tag chosen in one tab is still chosen in the next, where nothing carries it - which is the
        // one way this page narrows itself to nothing without the folder being empty.
        Services.GetRequiredService<FolderState>().Choose(FolderPage.Notes, FolderKey.Of(BuiltInFolder.Private));
        cut.Render();

        Assert.Contains("No notes carry those tags.", cut.Markup);
        Assert.DoesNotContain("Nothing in this folder.", cut.Markup);
    }

    /// <summary>Nothing is tagged, so there is nothing to narrow by and no field offering to.</summary>
    [Fact]
    public void A_page_of_untagged_notes_offers_no_tag_filter()
    {
        RegisterNotesApiClient([Note("Shopping"), Note("Ideas")]);

        var cut = RenderComponent<Web.Pages.Notes>();

        Assert.Empty(cut.FindAll(".value-browser"));
    }

    [Fact]
    public void A_pinned_note_is_listed_first()
    {
        RegisterNotesApiClient([Note("Shopping"), Note("Ideas") with { IsPinned = true }]);

        var cut = RenderComponent<Web.Pages.Notes>();

        var titles = cut.FindAll(".item-card-name").Select(element => element.TextContent.Trim()).ToList();
        Assert.Equal(["Ideas", "Shopping"], titles);
    }

    /// <summary>
    /// A recipient gets a pin of their own, and the same control the owner gets: the answer travels on
    /// the share row rather than on the note, so the page has one control and one flag to read - see
    /// NoteShare.IsPinnedByRecipient. What matters here is that it is offered at all: it used to be left
    /// out, and a note somebody sent you could not be brought to the top of your own page.
    /// </summary>
    [Fact]
    public void A_note_shared_with_you_can_be_pinned_by_the_reader()
    {
        RegisterNotesApiClient([Note("Shopping") with { IsShared = true, SharedByUserName = "anna" }]);

        var cut = RenderComponent<Web.Pages.Notes>();

        Assert.Single(cut.FindAll(".pin-button"));
    }

    /// <summary>
    /// And a shared note that arrives pinned leads the page like any other. Which reader's answer that
    /// flag carries is the server's to decide and is pinned down there
    /// (NotePinTests.A_recipients_pin_is_theirs_and_the_owner_is_not_told_about_it); this page reads one
    /// flag and asks no question about whose it is, which is the whole point of moving it.
    /// </summary>
    [Fact]
    public void A_shared_note_this_reader_pinned_leads_the_page()
    {
        RegisterNotesApiClient(
        [
            Note("Ideas"),
            Note("Shopping") with { IsShared = true, SharedByUserName = "anna", IsPinned = true }
        ]);

        var cut = RenderComponent<Web.Pages.Notes>();

        Assert.Single(cut.FindAll(".item-card.item-card-pinned"));
        var titles = cut.FindAll(".item-card-name").Select(element => element.TextContent.Trim()).ToList();
        Assert.Equal(["Shopping", "Ideas"], titles);
    }

    [Fact]
    public void An_account_with_no_notes_says_so()
    {
        RegisterNotesApiClient([]);

        var cut = RenderComponent<Web.Pages.Notes>();

        Assert.Contains("No notes.", cut.Markup);
    }

    [Fact]
    public void A_note_is_previewed_by_its_first_line_and_how_much_else_there_is()
    {
        RegisterNotesApiClient([Note("Shopping", "Milk", "Bread", "Coffee")]);

        var cut = RenderComponent<Web.Pages.Notes>();

        // The first line, not the whole note glued together - a preview that prints everything is not a
        // preview, and made every card in the list a different height.
        var preview = cut.Find(".item-card-body p").TextContent;
        Assert.Contains("Milk", preview);
        Assert.DoesNotContain("Coffee", preview);
        Assert.Contains("+2 more", preview);
    }

    [Fact]
    public void A_checklist_line_is_previewed_with_whether_it_is_done()
    {
        RegisterNotesApiClient([Note("Shopping") with
        {
            Content = [new NoteContentLineDto("Milk", IsChecklistItem: true, IsChecked: true)]
        }]);

        var cut = RenderComponent<Web.Pages.Notes>();

        // The same mark a task list preview uses, so a ticked-off line reads as ticked off here too.
        Assert.Contains("✓", cut.Find(".item-card-body p").TextContent);
        Assert.Contains("completed", cut.Find(".item-card-body p").ClassName);
    }

    [Fact]
    public void A_note_with_no_content_still_lists_without_a_preview()
    {
        RegisterNotesApiClient([Note("Empty one")]);

        var cut = RenderComponent<Web.Pages.Notes>();

        Assert.Contains("Empty one", cut.Markup);
        // Nothing written yet means no preview line at all, rather than an empty one holding space open.
        Assert.Empty(cut.FindAll(".item-card-body p"));
    }

    /// <summary>
    /// The card opens the note to be read, and changing what it says is a named press - the same two
    /// depths a task list and a storage have, see CalendarEventSummary.razor.
    /// </summary>
    [Fact]
    public void A_card_opens_the_note_and_its_menu_opens_the_form()
    {
        var note = Note("Shopping");
        RegisterNotesApiClient([note]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<Web.Pages.Notes>();

        cut.Find(".item-card-name").Click();
        Assert.EndsWith($"/notes/{note.Id}", navigationManager.Uri);

        OpenTheCardMenu(cut);
        FindButton(cut, "Edit").Click();

        Assert.EndsWith($"/notes/{note.Id}/edit", new Uri(navigationManager.Uri).AbsolutePath);
    }

    /// <summary>
    /// A sealed note's card opens it too. Its body is empty here - the server holds no readable line of
    /// it - but the empty box still sits over the card, so a press there used to land on nothing at all
    /// and the card read as broken. The light view opens it: the client unseals it there.
    /// </summary>
    [Fact]
    public void A_private_notes_card_opens_it_as_any_other_does()
    {
        var note = Note("Shopping") with { IsPrivate = true };
        RegisterNotesApiClient([note]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        // A sealed note is in Private, which is where somebody looking for it goes - see BuiltInFolder.
        Services.GetRequiredService<FolderState>().Choose(FolderPage.Notes, FolderKey.Of(BuiltInFolder.Private));
        var cut = RenderComponent<Web.Pages.Notes>();

        cut.Find(".item-card-body").Click();

        Assert.EndsWith($"/notes/{note.Id}", navigationManager.Uri);
    }

    /// <summary>
    /// The menu is named for what the press will do. A note shared read-only opens in the form to be
    /// looked at, and the list called that "Edit" while the note's own page called it "View" - two
    /// different things said about one note.
    /// </summary>
    [Fact]
    public void A_note_this_reader_cannot_change_offers_to_be_viewed()
    {
        RegisterNotesApiClient([
            Note("Theirs") with { IsShared = true, SharedByUserName = "Anna", AccessLevel = "ReadOnly" }]);
        var cut = RenderComponent<Web.Pages.Notes>();

        OpenTheCardMenu(cut);

        var offered = cut.FindAll(".avatar-dropdown-item").Select(entry => entry.TextContent.Trim()).ToList();
        Assert.Contains("View", offered);
        Assert.DoesNotContain("Edit", offered);
    }

    [Fact]
    public void Adding_a_note_opens_a_blank_one()
    {
        RegisterNotesApiClient([]);
        var cut = RenderComponent<Web.Pages.Notes>();

        cut.Find(".page-add").Click();

        Assert.EndsWith("/notes/new", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Deleting_a_note_asks_first_and_removes_it_once_confirmed()
    {
        var note = Note("Shopping") with { IsArchived = true };
        RegisterNotesApiClient([note], confirmDeletion: true);
        var cut = ReadingTheArchive();

        OpenTheCardMenu(cut);
        FindButton(cut, "Delete").Click();

        Assert.DoesNotContain("Shopping", cut.Markup);
        Assert.Contains(_requests, request => request.Method == HttpMethod.Delete);
    }

    [Fact]
    public void Declining_the_confirmation_leaves_the_note_alone()
    {
        RegisterNotesApiClient([Note("Shopping") with { IsArchived = true }], confirmDeletion: false);
        var cut = ReadingTheArchive();

        OpenTheCardMenu(cut);
        FindButton(cut, "Delete").Click();

        // Nothing asked of the server, and nothing removed from the page - a declined confirmation has
        // to mean the note is untouched, not merely that the row disappeared locally.
        Assert.Contains("Shopping", cut.Markup);
        Assert.DoesNotContain(_requests, request => request.Method == HttpMethod.Delete);
    }

    [Fact]
    public void An_expired_session_sends_the_reader_back_to_sign_in()
    {
        // The access token expired and refresh-and-retry failed too, so the page can't load - the reader
        // signs in again rather than sitting on a list that will never arrive.
        RegisterNotesApiClient(notes: null, statusCode: HttpStatusCode.Unauthorized);

        RenderComponent<Web.Pages.Notes>();

        Assert.EndsWith("/login", Services.GetRequiredService<NavigationManager>().Uri);
    }

    /// <summary>
    /// A note is handed to a contact from its own card. Everything else on this page could be done to
    /// one note at a time; sharing was the exception - it lived only in the bar over the list, so it
    /// took entering the choosing mode to hand on a single note. Asked for on 2026-09-18.
    /// </summary>
    [Fact]
    public void A_note_is_handed_to_a_contact_from_its_own_card()
    {
        RegisterNotesApiClient([Note("Shopping")]);
        var cut = RenderComponent<Web.Pages.Notes>();

        OpenTheCardMenu(cut);
        FindButton(cut, "Share").Click();

        Assert.NotEmpty(cut.FindAll(".dialog-panel"));
    }

    /// <summary>
    /// The same two questions the bar asks of everything it was given: a sealed note cannot be shared
    /// at all (the server refuses one), and a note shared *with* this reader belongs to whoever shared
    /// it. Neither offers the press rather than offering one that would be refused.
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void A_note_that_is_not_this_readers_to_hand_on_does_not_offer_it(bool isPrivate, bool isShared)
    {
        RegisterNotesApiClient([Note("Shopping") with { IsPrivate = isPrivate, IsShared = isShared }]);
        // A sealed note is drawn under Private rather than under the tab that opens - see FolderPlacement.
        var cut = isPrivate ? ReadingTheFolder(BuiltInFolder.Private) : RenderComponent<Web.Pages.Notes>();

        OpenTheCardMenu(cut);

        Assert.DoesNotContain(
            cut.FindAll(".avatar-dropdown-item"), entry => entry.TextContent.Trim() == "Share");
    }

    private static IElement FindButton(IRenderedFragment cut, string text)
        => cut.FindAll("button").First(button => button.TextContent.Trim() == text);

    /// <summary>
    /// Edit and Delete live in each card's overflow menu now, which has to be opened before they
    /// exist - see ItemCard's Menu slot and OverflowMenu.
    /// </summary>
    private static void OpenTheCardMenu(IRenderedFragment cut)
        => cut.FindAll(".overflow-menu-trigger").First().Click();

    private void RegisterNotesApiClient(
        IReadOnlyList<NoteDto>? notes, bool confirmDeletion = true, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(confirmDeletion);

        var handler = new StubHttpMessageHandler(request =>
        {
            _requests.Add(request);
            if (statusCode != HttpStatusCode.OK)
            {
                return new HttpResponseMessage(statusCode);
            }

            return request.Method == HttpMethod.Delete
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(notes ?? []) };
        });
        Services.AddSingleton(new NotesApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
    }

    /// <summary>
    /// A private note is named by its own title like any other. It travels sealed and this browser opens
    /// it on the way in (NotesApiClient.OpenIfPrivateAsync), so by the time the page has it the title is
    /// there to show - the page used to print "Private note" for every one of them, which left a column
    /// of identical rows nobody could tell apart.
    /// </summary>
    [Fact]
    public void A_private_note_is_listed_under_its_own_title()
    {
        RegisterNotesApiClient([Note("Passport") with { IsPrivate = true }, Note("Shopping")]);
        // A sealed note is in Private, which is where somebody looking for it goes - see BuiltInFolder.
        Services.GetRequiredService<FolderState>().Choose(FolderPage.Notes, FolderKey.Of(BuiltInFolder.Private));

        var cut = RenderComponent<Web.Pages.Notes>();

        Assert.Contains("Passport", cut.Markup);
        Assert.DoesNotContain("Private note", cut.Markup);
    }

    /// <summary>
    /// The fallback is still there for a note with no title at all - and for one sealed under a key pair
    /// that has since been replaced, which arrives already saying so and says it here.
    /// </summary>
    [Fact]
    public void A_note_with_no_title_is_still_named_for_what_it_is()
    {
        RegisterNotesApiClient([Note(string.Empty) with { IsPrivate = true }]);
        Services.GetRequiredService<FolderState>().Choose(FolderPage.Notes, FolderKey.Of(BuiltInFolder.Private));

        var cut = RenderComponent<Web.Pages.Notes>();

        Assert.Contains("Private note", cut.Markup);
    }

    /// <summary>
    /// The page with the Archived tab open, which is the only place Delete is offered at all - see
    /// ObjectMenu.IsArchived. A note that has been put away is only drawn under that tab anyway.
    /// </summary>
    private IRenderedComponent<Web.Pages.Notes> ReadingTheArchive()
        => ReadingTheFolder(BuiltInFolder.Archived);

    /// <summary>The page with one of the built-in tabs open - see FolderPlacement, which files into them.</summary>
    private IRenderedComponent<Web.Pages.Notes> ReadingTheFolder(BuiltInFolder folder)
    {
        var cut = RenderComponent<Web.Pages.Notes>();
        Services.GetRequiredService<FolderState>().Choose(FolderPage.Notes, FolderKey.Of(folder));
        cut.Render();
        return cut;
    }

    private static NoteDto Note(string title, params string[] lines)
        => new(
            Guid.NewGuid(), title,
            lines.Select(line => new NoteContentLineDto(line, IsChecklistItem: false, IsChecked: false)).ToList(),
            IsPrivate: false, EncryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);
}
