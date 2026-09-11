using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Chat;
using Orbit.Web.Components;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// The question asked before somebody leaves a group, on its own - the roster and the archive both show
/// it, so what it asks and what it sends is pinned here once rather than per page.
/// </summary>
public sealed class GroupLeaveConfirmationTests : OrbitTestContext
{
    private static readonly Guid GroupId = Guid.NewGuid();
    private static readonly Guid OwnUserId = Guid.NewGuid();
    private static readonly Guid AnnaUserId = Guid.NewGuid();
    private static readonly Guid PiotrUserId = Guid.NewGuid();

    private static readonly DateTimeOffset GroupStarted = DateTimeOffset.Parse("2026-08-01T10:00:00+00:00");

    /// <summary>Every request the component made, so a test can say what it actually asked for.</summary>
    private readonly List<(HttpMethod Method, string PathAndQuery)> _requests = [];

    private string? _refusal;

    public GroupLeaveConfirmationTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterSignedInUser();
        RegisterChatApi();
    }

    /// <summary>
    /// The only admin leaving people behind is asked who takes over, and the person already chosen is
    /// the one the server would pick by itself - the longest-standing, who here is listed second, so a
    /// picker that merely defaulted to the first row would fail.
    /// </summary>
    [Fact]
    public void The_only_admin_is_asked_who_takes_over_with_the_longest_standing_member_chosen()
    {
        var cut = Render(Group("Admin",
            Member(AnnaUserId, "Member", GroupStarted.AddDays(3)),
            Member(PiotrUserId, "Member", GroupStarted.AddDays(1))));

        Assert.Contains("Who takes over?", cut.Markup);
        Assert.Equal(PiotrUserId.ToString(), cut.Find("#successorInput").GetAttribute("value"));
    }

    [Fact]
    public void Confirming_sends_the_chosen_successor_and_says_the_reader_is_out()
    {
        var left = false;
        var cut = Render(
            Group("Admin",
                Member(AnnaUserId, "Member", GroupStarted.AddDays(3)),
                Member(PiotrUserId, "Member", GroupStarted.AddDays(1))),
            onLeft: () => left = true);

        cut.Find("#successorInput").Change(AnnaUserId.ToString());
        cut.Find(".btn-danger").Click();

        Assert.Contains(_requests, request => request.Method == HttpMethod.Delete
            && request.PathAndQuery == $"/api/chat/groups/{GroupId}/membership?successorUserId={AnnaUserId}");
        Assert.True(left);
    }

    /// <summary>
    /// Another admin stays behind, so the group is not left without anybody to manage it and there is
    /// nobody to choose - the same line the server draws before it promotes anyone.
    /// </summary>
    [Fact]
    public void An_admin_who_leaves_another_admin_behind_is_not_asked_who_takes_over()
    {
        var cut = Render(Group("Admin", Member(AnnaUserId, "Admin", GroupStarted.AddDays(1))));

        Assert.Empty(cut.FindAll("#successorInput"));
    }

    /// <summary>Everybody is asked to confirm, and a plain member is asked nothing more.</summary>
    [Fact]
    public void A_plain_member_is_asked_to_confirm_and_leaves_without_naming_anybody()
    {
        var cut = Render(Group("Member", Member(AnnaUserId, "Admin", GroupStarted.AddDays(1))));

        Assert.Contains("Your copies of its messages go with you", cut.Markup);
        Assert.Empty(cut.FindAll("#successorInput"));
        cut.Find(".btn-danger").Click();

        Assert.Contains(_requests, request => request.Method == HttpMethod.Delete
            && request.PathAndQuery == $"/api/chat/groups/{GroupId}/membership");
    }

    [Fact]
    public void The_last_one_out_is_told_the_group_goes_with_them()
    {
        var cut = Render(Group("Admin"));

        Assert.Contains("the group is deleted when you go", cut.Markup);
        Assert.Empty(cut.FindAll("#successorInput"));
    }

    /// <summary>
    /// A refusal is said in the server's words - "check your connection" would send somebody looking for
    /// a fault they do not have - and the page is asked to read the group again, so the picker offers
    /// who is actually there now.
    /// </summary>
    [Fact]
    public void A_refusal_is_said_in_the_servers_words_and_the_page_rereads_the_group()
    {
        _refusal = "The person you chose to take over isn't in this group any more.";
        var left = false;
        var refused = false;
        var cut = Render(
            Group("Admin", Member(AnnaUserId, "Member", GroupStarted.AddDays(1))),
            onLeft: () => left = true, onRefused: () => refused = true);

        cut.Find(".btn-danger").Click();

        Assert.Contains("isn't in this group any more", cut.Find("p.error").TextContent);
        Assert.True(refused);
        Assert.False(left);
    }

    [Fact]
    public void Cancelling_says_so_and_sends_nothing()
    {
        var cancelled = false;
        var cut = Render(Group("Member", Member(AnnaUserId, "Admin", GroupStarted)), onCancel: () => cancelled = true);

        cut.Find(".btn-secondary").Click();

        Assert.True(cancelled);
        Assert.DoesNotContain(_requests, request => request.Method == HttpMethod.Delete);
    }

    private IRenderedComponent<GroupLeaveConfirmation> Render(
        ChatGroupDto group, Action? onLeft = null, Action? onCancel = null, Action? onRefused = null)
        => RenderComponent<GroupLeaveConfirmation>(parameters => parameters
            .Add(component => component.Group, group)
            .Add(component => component.Contacts, [Contact(AnnaUserId, "Anna Kowalska"), Contact(PiotrUserId, "Piotr Nowak")])
            .Add(component => component.OnLeft, onLeft ?? (() => { }))
            .Add(component => component.OnCancel, onCancel ?? (() => { }))
            .Add(component => component.OnRefused, onRefused ?? (() => { })));

    /// <summary>A group with the reader in it - joined first, as its creator - and whoever else is given.</summary>
    private static ChatGroupDto Group(string ownRole, params ChatGroupMemberDto[] others)
        => new(GroupId, "Weekend trip", OwnUserId, GroupStarted, ownRole,
            [Member(OwnUserId, ownRole, GroupStarted), .. others]);

    private static ChatGroupMemberDto Member(Guid userId, string role, DateTimeOffset joinedAtUtc)
        => new(userId, role, joinedAtUtc);

    private static ContactDto Contact(Guid userId, string displayName)
        => new(userId, displayName.ToLowerInvariant(), displayName, "someone@example.test", "key", GroupStarted, false, false);

    /// <summary>Answers the leave route with no content, or with the refusal a test has set.</summary>
    private void RegisterChatApi()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            _requests.Add((request.Method, request.RequestUri!.PathAndQuery));
            if (_refusal is not null)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new { message = _refusal }), Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new ChatApiClient(httpClient));
    }

    /// <summary>Who is reading - the component leaves them out of the people who could take over.</summary>
    private void RegisterSignedInUser()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        tokenStore.SetTokenAsync(SignedInAs(OwnUserId)).GetAwaiter().GetResult();
        var refreshHttpClient = new HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new OrbitAuthenticationStateProvider(
            tokenStore, new TokenRefreshService(tokenStore, refreshHttpClient)));
    }

    /// <summary>An unsigned token naming the reader - the client reads the claims and never checks the signature.</summary>
    internal static string SignedInAs(Guid userId)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string>
        {
            ["sub"] = userId.ToString(),
            ["email"] = "owner@example.com",
            ["name"] = "Test Owner"
        }));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
