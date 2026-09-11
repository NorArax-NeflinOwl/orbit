using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Chat;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Sharing;
using Orbit.Contracts.Users;
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

    /// <summary>Who is signed in, kept so a client built later can be given it without resolving anything.</summary>
    private readonly OrbitAuthenticationStateProvider _authenticationStateProvider;

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
        _authenticationStateProvider = authenticationStateProvider;
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

    /// <summary>
    /// A note made while the Private tab is open starts sealed: being sealed is what puts a note under
    /// that tab, so one written there in the open would land under Public instead.
    /// </summary>
    [Fact]
    public void A_note_made_on_the_Private_tab_starts_sealed()
    {
        RegisterApiClients(note: null);
        Services.GetRequiredService<FolderState>().Choose(
            Orbit.Core.Folders.FolderPage.Notes, Orbit.Core.Folders.FolderKey.Of(Orbit.Core.Folders.BuiltInFolder.Private));

        var cut = RenderComponent<NoteEditor>();

        cut.Find(".editor-rail .overflow-menu-trigger").Click();
        Assert.True(cut.Find(".editor-settings-menu input[type=checkbox]").HasAttribute("checked"));
    }

    /// <summary>And one made under Public starts in the open, as every note did before.</summary>
    [Fact]
    public void A_note_made_on_the_Public_tab_starts_in_the_open()
    {
        RegisterApiClients(note: null);

        var cut = RenderComponent<NoteEditor>();

        cut.Find(".editor-rail .overflow-menu-trigger").Click();
        Assert.False(cut.Find(".editor-settings-menu input[type=checkbox]").HasAttribute("checked"));
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

    /// <summary>
    /// Both halves of handing a note over: the share the server records, and the sealed message that
    /// carries its id - the only thing a recipient can press "Accept" on (see Chat.razor's
    /// TryParseShare). The server cannot send the second, holding no key to seal it with, so a screen
    /// that forgets it shares something nobody can accept - which is exactly what one of the five
    /// screens that send it did for as long as it did.
    /// </summary>
    [Fact]
    public void Sharing_a_note_records_it_and_puts_the_invitation_in_the_conversation()
    {
        TheBrowserCanSeal();
        var note = Note("Shopping");
        RegisterApiClients(note, [Contact]);
        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, note.Id));

        cut.Find("#shareContactSelect").Change(ContactUserId.ToString());
        cut.Find("#shareNoteButton").Click();

        Assert.True(_wasShared);
        Assert.NotNull(_lastChatMessageJson);
        // Marked as an invitation, which is what makes the chat draw it with an Accept beside it.
        Assert.Contains("\"isShareInvitation\":true", _lastChatMessageJson);
    }

    /// <summary>Nobody chosen is nothing to send, and nothing shared either.</summary>
    [Fact]
    public void Sharing_a_note_with_nobody_sends_nothing()
    {
        TheBrowserCanSeal();
        var note = Note("Shopping");
        RegisterApiClients(note, [Contact]);
        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, note.Id));

        cut.Find("#shareNoteButton").Click();

        Assert.False(_wasShared);
        Assert.Null(_lastChatMessageJson);
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

        // What the note is rather than what is in it lives in the panel's menu now - see NoteEditor.
        cut.Find(".editor-rail .overflow-menu-trigger").Click();
        cut.Find(".editor-settings-menu input[type=checkbox]").Change(true);

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
    /// A note held read-only cannot be typed into. The disabled fieldset around the form does not say
    /// so on its own: it disables form controls, and the writing surface is a contenteditable div,
    /// which is not one - so the note took every keystroke with Save greyed out beside it.
    /// </summary>
    [Fact]
    public void A_note_shared_read_only_cannot_be_written_in()
    {
        var note = Note("Shopping") with
        {
            IsShared = true, SharedByUserName = "anna", AccessLevel = "ReadOnly"
        };
        RegisterApiClients(note);

        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, note.Id));

        var surface = cut.Find(".note-editor-content");
        Assert.Equal("false", surface.GetAttribute("contenteditable"));
        Assert.Contains("note-editor-content-readonly", surface.ClassName);
    }

    /// <summary>And neither can one somebody else is holding - the same surface, the same rule.</summary>
    [Fact]
    public void A_note_someone_else_is_editing_cannot_be_written_in()
    {
        var note = Note("Shopping");
        RegisterApiClients(note, lockedByUserName: "anna");

        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, note.Id));

        Assert.Equal("false", cut.Find(".note-editor-content").GetAttribute("contenteditable"));
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
    /// The row of tools sits over the corner of the writing rather than above it, and three of its four
    /// are drawn for a design that has them rather than for anything they do yet. Each says so when it
    /// is pressed: a greyed-out button explains nothing, and a row of them explains less.
    /// </summary>
    [Fact]
    public void The_tools_over_the_writing_say_when_there_is_nothing_behind_them()
    {
        var note = Note("Shopping");
        RegisterApiClients(note);
        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, note.Id));

        Assert.Equal(4, cut.FindAll(".note-editor-tools .note-tool").Count);
        Assert.Empty(cut.FindAll(".note-tool-bubble"));

        cut.FindAll(".note-editor-tools .note-tool")
            .First(tool => tool.GetAttribute("aria-label") == "Table").Click();

        Assert.Contains("not implemented yet", cut.Find(".note-tool-bubble").TextContent);
    }

    /// <summary>
    /// What the note is rather than what is in it - how much it matters, where it is filed, whether it
    /// is sealed - is in the panel's menu, above Save and Back. It used to sit under the writing, which
    /// is a form somebody had to scroll past to reach the end of what they were writing.
    /// </summary>
    [Fact]
    public void What_the_note_is_lives_in_the_panels_menu()
    {
        var note = Note("Shopping");
        RegisterApiClients(note);
        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, note.Id));

        Assert.Empty(cut.FindAll(".editor-settings-menu"));

        cut.Find(".editor-rail .overflow-menu-trigger").Click();

        var menu = cut.Find(".editor-rail .editor-settings-menu");
        Assert.Contains("Priority", menu.TextContent);
        Assert.Contains("Folder", menu.TextContent);
        Assert.Contains("Private", menu.TextContent);
    }

    /// <summary>
    /// The notes in the same folder stand beside the one being written, most recently changed first -
    /// what is being written is one of a set, and moving between them should not mean going back to a
    /// page of cards each time. A note filed somewhere else is not in that set.
    /// </summary>
    [Fact]
    public void The_notes_in_the_same_folder_stand_beside_the_one_being_written()
    {
        var folderId = Guid.NewGuid();
        var note = Note("Shopping") with { FolderId = folderId, UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1) };
        var newer = Note("Packing") with { FolderId = folderId, UpdatedAtUtc = DateTimeOffset.UtcNow };
        var elsewhere = Note("Work") with { FolderId = Guid.NewGuid() };
        RegisterApiClients(note, alsoInTheList: [newer, elsewhere]);

        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, note.Id));

        var rows = cut.FindAll(".note-workspace-row");
        Assert.Equal(["Packing", "Shopping"], rows.Select(row => row.QuerySelector(".note-workspace-row-title")!.TextContent));
        // The one being written in is marked rather than left out: a column that hides the note you are
        // reading answers "where am I" with nothing.
        Assert.Contains("chosen", rows.First(row => row.TextContent.Contains("Shopping")).ClassList);
    }

    [Fact]
    public void Pressing_a_note_beside_the_writing_opens_it()
    {
        var folderId = Guid.NewGuid();
        var note = Note("Shopping") with { FolderId = folderId };
        var other = Note("Packing") with { FolderId = folderId };
        RegisterApiClients(note, alsoInTheList: [other]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<NoteEditor>(parameters => parameters.Add(editor => editor.Id, note.Id));

        cut.FindAll(".note-workspace-row").First(row => row.TextContent.Contains("Packing")).Click();

        Assert.EndsWith($"/notes/{other.Id}/edit", navigationManager.Uri);
    }

    /// <summary>
    /// Answers the editor's whole load sequence from one place: the note itself, the lock it tries to
    /// take, and the contacts the sharing picker offers.
    /// </summary>
    /// <param name="alsoInTheList">
    /// The account's other notes, for the column beside the writing - see NoteEditor's
    /// ListTheNotesBesideItAsync. Left out by every test that is not about that column.
    /// </param>
    private void RegisterApiClients(
        NoteDto? note, IReadOnlyList<ContactDto>? contacts = null, string? lockedByUserName = null,
        IReadOnlyList<NoteDto>? alsoInTheList = null)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            // Handing the note to somebody: the share itself, and the sealed message that carries the
            // "Accept" - see EncryptedChatMessageSender, and the note about what the server cannot do.
            if (request.Method == HttpMethod.Post && path.EndsWith("/shares", StringComparison.Ordinal))
            {
                _wasShared = true;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new ShareResultDto(Guid.NewGuid(), AlreadyShared: false))
                };
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/chat/messages", StringComparison.Ordinal))
            {
                _lastChatMessageJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            // Somebody who has signed in at least once, so there is a key to seal an invitation with.
            if (path.StartsWith("/api/users/", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        new UserSearchResultDto(ContactUserId, "anna", "Anna Kowalska", "a-public-key"))
                };
            }

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

            // The column of notes beside the writing asks for the lot - see NoteEditor's
            // ListTheNotesBesideItAsync. Answered as a list rather than as the one note, the way the
            // server answers it: a fake that hands a single object back where an array is expected
            // fails the page for a reason no reader would ever see.
            if (path.TrimEnd('/').EndsWith("/api/notes", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create<IReadOnlyList<NoteDto>>(
                        [.. note is null ? [] : new[] { note }, .. alsoInTheList ?? []])
                };
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
        // Over the one the constructor registered, so the sealed message goes somewhere a test can read
        // it rather than at a client with no handler behind it at all.
        var usersApiClient = new UsersApiClient(httpClient);
        Services.AddSingleton(new EncryptedChatMessageSender(
            JSInterop.JSRuntime,
            new OwnEncryptionKeyProvider(JSInterop.JSRuntime, usersApiClient, _authenticationStateProvider),
            usersApiClient,
            new ChatApiClient(httpClient)));
    }

    /// <summary>What the share wrote into the conversation, and whether the share itself was recorded.</summary>
    private string? _lastChatMessageJson;
    private bool _wasShared;

    /// <summary>
    /// The browser's half of the encryption, stood in for: this device holds a key, and sealing answers
    /// with something. Mirrors ShareInventoryPanelTests, where the same three calls are planned - the
    /// crypto itself is checked in a real browser by ci/verify-browser-crypto.mjs.
    /// </summary>
    private void TheBrowserCanSeal()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var crypto = JSInterop.SetupModule("./js/e2eeChat.js");
        crypto.Setup<bool>("hasOwnPrivateKey", _ => true).SetResult(true);
        crypto.Setup<string>("ensureOwnPublicKey", _ => true).SetResult("a-public-key");
        crypto.Setup<EncryptedChatMessageSender.EncryptedPayload>("encryptMessage", _ => true)
            .SetResult(new EncryptedChatMessageSender.EncryptedPayload("sealed", "nonce"));
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
