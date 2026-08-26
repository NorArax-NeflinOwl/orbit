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
    public void An_account_with_no_notes_says_so()
    {
        RegisterNotesApiClient([]);

        var cut = RenderComponent<Web.Pages.Notes>();

        Assert.Contains("No notes.", cut.Markup);
    }

    [Fact]
    public void A_notes_lines_are_previewed_as_one_line_of_text()
    {
        // Content is structured rather than a flat string, so the preview has to join it back up -
        // otherwise a multi-line note would render its lines run together with no spaces.
        RegisterNotesApiClient([Note("Shopping", "Milk", "Bread", "Coffee")]);

        var cut = RenderComponent<Web.Pages.Notes>();

        Assert.Contains("Milk Bread Coffee", cut.Find("li p").TextContent);
    }

    [Fact]
    public void A_note_with_no_content_still_lists_without_a_preview()
    {
        RegisterNotesApiClient([Note("Empty one")]);

        var cut = RenderComponent<Web.Pages.Notes>();

        Assert.Contains("Empty one", cut.Markup);
        Assert.Equal(string.Empty, cut.Find("li p").TextContent.Trim());
    }

    [Fact]
    public void Editing_a_note_opens_it()
    {
        var note = Note("Shopping");
        RegisterNotesApiClient([note]);
        var cut = RenderComponent<Web.Pages.Notes>();

        FindButton(cut, "Edit").Click();

        Assert.EndsWith($"/notes/{note.Id}", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Adding_a_note_opens_a_blank_one()
    {
        RegisterNotesApiClient([]);
        var cut = RenderComponent<Web.Pages.Notes>();

        FindButton(cut, "Add note").Click();

        Assert.EndsWith("/notes/new", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Deleting_a_note_asks_first_and_removes_it_once_confirmed()
    {
        var note = Note("Shopping");
        RegisterNotesApiClient([note], confirmDeletion: true);
        var cut = RenderComponent<Web.Pages.Notes>();

        FindButton(cut, "Delete").Click();

        Assert.DoesNotContain("Shopping", cut.Markup);
        Assert.Contains(_requests, request => request.Method == HttpMethod.Delete);
    }

    [Fact]
    public void Declining_the_confirmation_leaves_the_note_alone()
    {
        RegisterNotesApiClient([Note("Shopping")], confirmDeletion: false);
        var cut = RenderComponent<Web.Pages.Notes>();

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
