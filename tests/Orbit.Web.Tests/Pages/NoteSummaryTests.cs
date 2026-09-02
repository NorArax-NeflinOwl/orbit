using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Notes;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// A note read rather than written, with the checklist lines in it tickable. Opening a text editor to
/// tick one is the same mismatch a shelf had before it could be counted.
/// </summary>
public sealed class NoteSummaryTests : OrbitTestContext
{
    private static readonly Guid NoteId = Guid.NewGuid();

    private readonly List<string> _saved = [];
    private NoteDto _note = ANote();

    public NoteSummaryTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Put)
            {
                _saved.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(_note) };
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        // No sealer: these notes are not private, and a private one is opened and resealed by the
        // client rather than by this page - see NotesApiClient.
        Services.AddSingleton(new NotesApiClient(httpClient));
    }

    [Fact]
    public void The_note_is_read_and_its_checklist_lines_are_tickable()
    {
        var cut = RenderComponent<NoteSummary>(parameters => parameters.Add(page => page.Id, NoteId));

        // Prose stays prose; the list in it is a list.
        Assert.Contains("Before the weekend", cut.Find(".card").TextContent);
        Assert.Equal(2, cut.FindAll(".check-row input[type=checkbox]").Count);
    }

    [Fact]
    public void Ticking_a_line_writes_nothing_until_it_is_saved()
    {
        var cut = RenderComponent<NoteSummary>(parameters => parameters.Add(page => page.Id, NoteId));
        var save = cut.FindAll("button").First(button => button.GetAttribute("aria-label") == "Save");
        Assert.True(save.HasAttribute("disabled"));

        cut.FindAll(".check-row input[type=checkbox]").First().Change(true);

        // A note is read by scrolling: a page that saved on every press would be writing while somebody
        // is only passing through.
        Assert.Empty(_saved);

        cut.FindAll("button").First(button => button.GetAttribute("aria-label") == "Save").Click();

        var written = Assert.Single(_saved);
        Assert.Contains("\"isChecked\":true", written);
        // And the rest of the note went with it, unchanged.
        Assert.Contains("Before the weekend", written);
    }

    [Fact]
    public void Rewriting_it_is_a_named_press()
    {
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<NoteSummary>(parameters => parameters.Add(page => page.Id, NoteId));

        cut.Find(".editor-rail .overflow-menu-trigger").Click();
        cut.FindAll(".avatar-dropdown-item").First(entry => entry.TextContent.Trim() == "Edit").Click();

        Assert.EndsWith($"/notes/{NoteId}/edit", navigationManager.Uri);
    }

    /// <summary>
    /// The two panels an editing screen is made of: what is read and filled in on the left, and the
    /// actions on the right, outside the box that scrolls. The pair used to sit in the page's heading,
    /// which scrolled away with it - see EditorRail.
    /// </summary>
    [Fact]
    public void The_actions_sit_outside_what_scrolls()
    {
        var cut = RenderComponent<NoteSummary>(parameters => parameters.Add(page => page.Id, NoteId));

        Assert.NotEmpty(cut.FindAll(".editor-page > .editor-rail"));
        Assert.Empty(cut.FindAll(".editor-page-body .editor-rail"));
    }

    /// <summary>Somebody else's note, held read-only: it can be read here and not ticked.</summary>
    [Fact]
    public void A_read_only_share_can_be_read_and_not_ticked()
    {
        _note = ANote() with { IsShared = true, SharedByUserName = "Anna", AccessLevel = "ReadOnly" };

        var cut = RenderComponent<NoteSummary>(parameters => parameters.Add(page => page.Id, NoteId));

        Assert.All(
            cut.FindAll(".check-row input[type=checkbox]"),
            checkbox => Assert.True(checkbox.HasAttribute("disabled")));
    }

    private static NoteDto ANote()
        => new(
            NoteId, "Shopping",
            [
                new NoteContentLineDto("Before the weekend", IsChecklistItem: false, IsChecked: false),
                new NoteContentLineDto("Milk", IsChecklistItem: true, IsChecked: false),
                new NoteContentLineDto("Bread", IsChecklistItem: true, IsChecked: false)
            ],
            IsPrivate: false, EncryptedContent: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);
}
