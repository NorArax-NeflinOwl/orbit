using System.Net;
using System.Net.Http.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Notes;
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

    [Fact]
    public void Every_note_is_listed()
    {
        RegisterNotesApiClient([Note("Shopping"), Note("Ideas")]);

        var cut = RenderComponent<Web.Pages.Notes>();

        var markup = cut.Markup;
        Assert.Contains("Shopping", markup);
        Assert.Contains("Ideas", markup);
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
    /// A recipient gets a pin of their own. The owner's is on the note and moves it on the owner's page,
    /// so this one is kept on the reader's device instead - see SharedItemPins. What matters here is that
    /// the control is offered at all: it used to be left out, and a note somebody sent you could not be
    /// brought to the top of your own page.
    /// </summary>
    [Fact]
    public void A_note_shared_with_you_can_be_pinned_by_the_reader()
    {
        RegisterNotesApiClient([Note("Shopping") with { IsShared = true, SharedByUserName = "anna" }]);

        var cut = RenderComponent<Web.Pages.Notes>();

        Assert.Single(cut.FindAll(".pin-button"));
    }

    /// <summary>
    /// And it is the reader's answer that shows, not the owner's: a note its owner pinned on their page
    /// arrives here unpinned, because where it sits on this page is this reader's to say.
    /// </summary>
    [Fact]
    public void The_owners_pin_does_not_reach_a_reader_it_was_shared_with()
    {
        RegisterNotesApiClient(
        [
            Note("Shopping") with { IsShared = true, SharedByUserName = "anna", IsPinned = true },
            Note("Ideas")
        ]);

        var cut = RenderComponent<Web.Pages.Notes>();

        Assert.Empty(cut.FindAll(".item-card.item-card-pinned"));
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
    /// depths a task list and a storage have, see NoteSummary.razor.
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

        Assert.EndsWith($"/notes/{note.Id}/edit", navigationManager.Uri);
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
        var note = Note("Shopping");
        RegisterNotesApiClient([note], confirmDeletion: true);
        var cut = RenderComponent<Web.Pages.Notes>();

        OpenTheCardMenu(cut);
        FindButton(cut, "Delete").Click();

        Assert.DoesNotContain("Shopping", cut.Markup);
        Assert.Contains(_requests, request => request.Method == HttpMethod.Delete);
    }

    [Fact]
    public void Declining_the_confirmation_leaves_the_note_alone()
    {
        RegisterNotesApiClient([Note("Shopping")], confirmDeletion: false);
        var cut = RenderComponent<Web.Pages.Notes>();

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

    private static NoteDto Note(string title, params string[] lines)
        => new(
            Guid.NewGuid(), title,
            lines.Select(line => new NoteContentLineDto(line, IsChecklistItem: false, IsChecked: false)).ToList(),
            IsPrivate: false, EncryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);
}
