using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Chat;
using Orbit.Contracts.Inventories;
using Orbit.Contracts.Sharing;
using Orbit.Contracts.Users;
using Orbit.Web.Components;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// Sharing a storage, and specifically the half of it that had no coverage anywhere: the encrypted chat
/// message carrying the share's id, which is the only thing a recipient can press "Accept" on - the
/// server holds no key to seal one with. Four screens send it and none was covered, which is how one of
/// them came to send only half an invitation (see TaskEditorItemFormTests's guest test).
/// </summary>
public sealed class ShareInventoryPanelTests : OrbitTestContext
{
    private static readonly Guid OwnUserId = Guid.NewGuid();
    private static readonly Guid ContactUserId = Guid.NewGuid();
    private static readonly Guid InventoryId = Guid.NewGuid();

    private string? _lastChatMessageJson;
    private bool _wasShared;

    public ShareInventoryPanelTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            // Somebody who has signed in at least once, so there is a key to seal an invitation with.
            if (path.StartsWith("/api/users/", StringComparison.Ordinal))
            {
                return Json(new UserSearchResultDto(ContactUserId, "anna", "Anna Kowalska", "a-public-key"));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/shares", StringComparison.Ordinal))
            {
                _wasShared = true;
                return Json(new ShareResultDto(Guid.NewGuid(), AlreadyShared: false));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/api/chat/messages", StringComparison.Ordinal))
            {
                _lastChatMessageJson = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        Services.AddSingleton(new InventoryApiClient(httpClient));

        // Somebody has to be signed in: sealing a message asks this browser for its own key pair, and
        // that is looked up under the signed-in user's id. Unsigned - nothing here verifies a token.
        var tokenStore = new TokenStore(new StubJSRuntime());
        tokenStore.SetTokenAsync(AnUnsignedTokenFor(OwnUserId)).GetAwaiter().GetResult();
        var authenticationStateProvider = new OrbitAuthenticationStateProvider(
            tokenStore, new TokenRefreshService(tokenStore, httpClient));
        var usersApiClient = new UsersApiClient(httpClient);
        Services.AddSingleton(new EncryptedChatMessageSender(
            JSInterop.JSRuntime,
            new OwnEncryptionKeyProvider(JSInterop.JSRuntime, usersApiClient, authenticationStateProvider),
            usersApiClient,
            new ChatApiClient(httpClient)));
    }

    /// <summary>
    /// Both halves, and the second is the point: without it the recipient is told somebody shared a
    /// storage and has nothing to press.
    /// </summary>
    [Fact]
    public void Sharing_a_storage_records_it_and_puts_the_invitation_in_the_conversation()
    {
        TheBrowserCanSeal();
        var cut = Render();

        cut.Find("#shareContactSelect").Change(ContactUserId.ToString());
        cut.Find("#shareInventoryButton").Click();

        Assert.True(_wasShared);
        Assert.NotNull(_lastChatMessageJson);
        // Marked as an invitation, which is what makes the chat draw it with an Accept beside it rather
        // than as an ordinary message.
        Assert.Contains("\"isShareInvitation\":true", _lastChatMessageJson);
    }

    /// <summary>Nobody chosen is nothing to send - and nothing shared either.</summary>
    [Fact]
    public void Sharing_with_nobody_sends_nothing()
    {
        TheBrowserCanSeal();
        var cut = Render();

        cut.Find("#shareInventoryButton").Click();

        Assert.False(_wasShared);
        Assert.Null(_lastChatMessageJson);
    }

    private IRenderedComponent<ShareInventoryPanel> Render()
        => RenderComponent<ShareInventoryPanel>(parameters => parameters
            .Add(panel => panel.Inventory, new InventoryDto(
                InventoryId, "Pantry", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", LockedByUserName: null,
                OriginalOwnerUserId: null))
            .Add(panel => panel.Contacts, new[]
            {
                new ContactDto(
                    ContactUserId, "anna", "Anna Kowalska", "anna@example.com", "public-key",
                    DateTimeOffset.UtcNow, RequiresApprovalFromCurrentUser: false,
                    IsPendingApprovalFromOtherParty: false)
            })
            .Add(panel => panel.OwnUserId, OwnUserId));

    /// <summary>
    /// The browser's half of the encryption, stood in for: this device holds a key, and sealing answers
    /// with something. What is asserted is that the sealed thing was sent, not what it contains - the
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

    /// <summary>
    /// A token this browser reads a user id out of. Unsigned on purpose: nothing on this side verifies
    /// one, and the API this test talks to is a stub.
    /// </summary>
    private static string AnUnsignedTokenFor(Guid userId)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(
            $$"""{"sub":"{{userId}}","email":"owner@example.com","name":"Test Owner"}"""));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static HttpResponseMessage Json<T>(T body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}
