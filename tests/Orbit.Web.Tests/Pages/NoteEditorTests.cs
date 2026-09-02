using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Chat;
using Orbit.Contracts.Notes;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Orbit.Web.Tests;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// Covers what the note editor offers for a given note: whether it can be edited at all, whether it can
/// be shared, and the interaction between privacy and sharing - the rules the server also enforces, so
/// the page never offers something that would only come back refused.
/// </summary>
public sealed class NoteEditorTests : OrbitTestContext
{
    private static readonly Guid OwnUserId = Guid.NewGuid();
    private static readonly Guid ContactUserId = Guid.NewGuid();

    private static readonly ContactDto Contact =
        new(ContactUserId, "anna", "Anna Kowalska", "anna@example.com", "public-key", DateTimeOffset.UtcNow,
            RequiresApprovalFromCurrentUser: false, IsPendingApprovalFromOtherParty: false);

    public NoteEditorTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // The content field is a ChecklistTextEditor, which loads its own JS module and hands it the
        // note's lines. bUnit refuses any interop call it hasn't been told about, so both the module and
        // the calls it makes have to be declared - none of these tests exercise what that editor does,
        // they just can't render without it.
        var checklistEditorModule = JSInterop.SetupModule("./js/checklistTextEditor.js");
        checklistEditorModule.SetupVoid("initialize", _ => true).SetVoidResult();
        checklistEditorModule.SetupVoid("dispose", _ => true).SetVoidResult();

        // The same wiring CalendarEventEditorTests uses, for the same reason: the editor injects a
        // collaborator graph that only its save path exercises, and it just has to resolve.
        var tokenStore = new TokenStore(new StubJSRuntime());
        tokenStore.SetTokenAsync(CreateUnsignedJwt(new Dictionary<string, string>
        {
            ["sub"] = OwnUserId.ToString(),
            ["email"] = "owner@example.com",
            ["name"] = "Test Owner"
        })).GetAwaiter().GetResult();
        var refreshHttpClient = new HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var authenticationStateProvider = new OrbitAuthenticationStateProvider(
            tokenStore, new TokenRefreshService(tokenStore, refreshHttpClient));
        Services.AddSingleton(authenticationStateProvider);
        Services.AddSingleton<AuthenticationStateProvider>(authenticationStateProvider);
        Services.AddAuthorizationCore();

        var jsRuntime = JSInterop.JSRuntime;
        var usersApiClient = new UsersApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") });
        Services.AddSingleton(usersApiClient);
        var ownEncryptionKeyProvider = new OwnEncryptionKeyProvider(jsRuntime, usersApiClient, authenticationStateProvider);
        Services.AddSingleton(new EncryptedChatMessageSender(
            jsRuntime, ownEncryptionKeyProvider, usersApiClient,
            new ChatApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") })));
    }

    [Fact]
    public void A_new_note_opens_on_an_empty_form_with_nothing_to_share_yet()
    {
        RegisterApiClients(note: null);

        var cut = RenderComponent<NoteEditor>();

        Assert.Empty(FirstWrittenLine(cut));
        // Nothing exists to share until it has been saved once.
        Assert.DoesNotContain("Sharing", cut.Markup);
    }

    [Fact]
    public void An_existing_note_opens_with_its_title_in_place()
    {
        var note = Note("Shopping");
        RegisterApiClients(note);

        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, note.Id));

        Assert.Equal("Shopping", FirstWrittenLine(cut));
    }

    [Fact]
    public void A_note_you_own_offers_sharing()
    {
        var note = Note("Shopping");
        RegisterApiClients(note, [Contact]);

        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, note.Id));

        Assert.Contains("Sharing", cut.Markup);
        Assert.Contains("Anna Kowalska", cut.Markup);
    }

    [Fact]
    public void A_private_note_offers_no_sharing_at_all()
    {
        // The server refuses to share a private note; offering the form would only lead to a refusal.
        var note = Note("Passwords") with { IsPrivate = true };
        RegisterApiClients(note, [Contact]);

        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, note.Id));

        Assert.DoesNotContain("Sharing", cut.Markup);
    }

    [Fact]
    public void Ticking_Private_withdraws_the_sharing_form_there_and_then()
    {
        var note = Note("Shopping");
        RegisterApiClients(note, [Contact]);
        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, note.Id));
        Assert.Contains("Sharing", cut.Markup);

        cut.Find("input[type=checkbox]").Change(true);

        // Before saving, not after: the point is that the two are mutually exclusive, and the page says
        // so as soon as the choice is made rather than once the server has been told.
        Assert.DoesNotContain("Sharing", cut.Markup);
    }

    [Fact]
    public void A_note_shared_with_you_says_who_shared_it()
    {
        var note = Note("Their note") with { IsShared = true, SharedByUserName = "anna", AccessLevel = "ReadOnly" };
        RegisterApiClients(note);

        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, note.Id));

        Assert.Contains("anna", cut.Find(".shared-note-banner").TextContent);
    }

    [Fact]
    public void A_read_only_share_cannot_be_edited()
    {
        var note = Note("Their note") with { IsShared = true, SharedByUserName = "anna", AccessLevel = "ReadOnly" };
        RegisterApiClients(note);

        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, note.Id));

        Assert.True(cut.Find("fieldset").HasAttribute("disabled"));
    }

    [Fact]
    public void A_note_reached_through_a_share_cannot_be_made_private()
    {
        // "Private" means private to its creator, and a shared note belongs to someone else.
        var note = Note("Their note") with { IsShared = true, SharedByUserName = "anna", AccessLevel = "CanEdit" };
        RegisterApiClients(note);

        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, note.Id));

        Assert.Empty(cut.FindAll("input[type=checkbox]"));
    }

    /// <summary>
    /// Said in the panel that stays in view rather than above a form that scrolls away, and beside the
    /// Save it explains: "why is Save greyed" is asked with the thumb on Save. The panel is drawn for a
    /// note nobody can write to as well - one that vanished took the reason with it.
    /// </summary>
    [Fact]
    public void A_note_someone_else_is_editing_says_so_and_locks_the_form()
    {
        var note = Note("Shopping");
        RegisterApiClients(note, lockedByUserName: "anna");

        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, note.Id));

        Assert.Contains("anna", cut.Find(".editor-rail-extras .lock-banner").TextContent);
        Assert.True(cut.Find("fieldset").HasAttribute("disabled"));
        Assert.True(cut.Find(".page-action-primary").HasAttribute("disabled"));
    }

    [Fact]
    public void A_new_note_cannot_be_saved_until_it_has_something_in_it()
    {
        RegisterApiClients(note: null);

        var cut = RenderComponent<NoteEditor>();

        // Caught while the mistake is still being made, rather than left to fail on the server.
        Assert.True(cut.Find(".page-action-primary").HasAttribute("disabled"));
        Assert.Contains("Write something in it", cut.Markup);
    }

    [Fact]
    public void A_title_is_enough_to_save()
    {
        RegisterApiClients(note: null);
        var cut = RenderComponent<NoteEditor>();

        WriteFirstLine(cut, "Dentist on Tuesday");

        Assert.False(cut.Find(".page-action-primary").HasAttribute("disabled"));
        Assert.DoesNotContain("Write something in it", cut.Markup);
    }

    [Fact]
    public void An_existing_note_with_content_saves_normally()
    {
        RegisterApiClients(Note("Shopping"));

        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, Note("Shopping").Id));

        Assert.False(cut.Find(".page-action-primary").HasAttribute("disabled"));
    }

    /// <summary>
    /// What the note is called, which is the first line of the one field the editor has - there is no
    /// separate title box any more, so this reads it where it actually lives.
    /// </summary>
    private static string FirstWrittenLine(IRenderedComponent<NoteEditor> cut)
    {
        var lines = cut.FindComponent<Web.Components.ChecklistTextEditor>().Instance.Lines;
        return lines.Count > 0 ? lines[0].Text : string.Empty;
    }

    /// <summary>
    /// Types the first line. The field is a contenteditable driven by JS, which bUnit cannot type
    /// into, so this raises the same callback that JS raises after an edit.
    /// </summary>
    private static void WriteFirstLine(IRenderedComponent<NoteEditor> cut, string text)
    {
        var editor = cut.FindComponent<Web.Components.ChecklistTextEditor>();
        cut.InvokeAsync(() => editor.Instance.LinesChanged.InvokeAsync(
            [new Orbit.Contracts.Notes.NoteContentLineDto(text, false, false)])).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Answers the editor's whole load sequence from one place: the note itself, the lock it tries to
    /// take, and the contacts the sharing picker offers.
    /// </summary>
    private void RegisterApiClients(NoteDto? note, IReadOnlyList<ContactDto>? contacts = null, string? lockedByUserName = null)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/lock", StringComparison.Ordinal))
            {
                return lockedByUserName is null
                    ? new HttpResponseMessage(HttpStatusCode.NoContent)
                    : new HttpResponseMessage(HttpStatusCode.Conflict)
                    {
                        Content = JsonContent.Create(new { lockedByUserName })
                    };
            }

            // ShareLinkButton asks on render whether this item already has a public link. NoContent is
            // "no link yet", which is what these tests want - none of them are about publishing.
            if (path.StartsWith("/api/share-links", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.StartsWith("/api/notes", StringComparison.Ordinal))
            {
                return note is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(note) };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(contacts ?? []) };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new NotesApiClient(httpClient));
        Services.AddSingleton(new PublicShareApiClient(httpClient));
        Services.AddSingleton(new ChatApiClient(httpClient));
    }

    /// <summary>Mirrors CalendarEventEditorTests - a real header and payload with a dummy signature the client never checks.</summary>
    private static string CreateUnsignedJwt(Dictionary<string, string> claims)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static NoteDto Note(string title)
        => new(
            Guid.NewGuid(), title, [new NoteContentLineDto("A line", IsChecklistItem: false, IsChecked: false)],
            IsPrivate: false, EncryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);
}
