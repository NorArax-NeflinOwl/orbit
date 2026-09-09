using Orbit.Core.Permissions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Permissions;
using Orbit.Mobile.Screens.About;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The screen the drawer's About entry opens: what Orbit is, which build this one is, and the
/// documents this deployment publishes about itself.
///
/// It used to be three lines folded into the foot of the drawer, and what it could not hold there was
/// the row of documents Orbit.Web's footer has carried all along - see AboutViewModel.
/// </summary>
public sealed class AboutScreenTests
{
    /// <summary>
    /// The commit is detail about Orbit's own inside, so it goes to the accounts holding Debug and to
    /// nobody else - the rule the server and the browser's footer already apply. The number itself
    /// stays: "which Orbit is this" is everybody's question.
    /// </summary>
    [Fact]
    public async Task The_version_keeps_the_commit_from_an_account_without_debug()
    {
        using var localStore = new LocalStore();
        var about = Open(await UnlockedPermissions.LockedTo(localStore, ApplicationPermission.Contacts));
        await about.LoadCommand.ExecuteAsync(null);

        Assert.DoesNotContain("gitHash", about.Version);
        Assert.Contains("ver:", about.Version);
        // Nothing to reveal, so the line is text rather than something that looks pressable.
        Assert.False(about.CanShowTheWholeCommit);
    }

    /// <inheritdoc cref="The_version_keeps_the_commit_from_an_account_without_debug"/>
    [Fact]
    public async Task The_version_says_the_commit_to_an_account_holding_debug()
    {
        using var localStore = new LocalStore();
        var about = Open(await UnlockedPermissions.LockedTo(localStore, ApplicationPermission.Debug));
        await about.LoadCommand.ExecuteAsync(null);

        Assert.Contains("gitHash", about.Version);
    }

    /// <summary>
    /// A build told no web address has nowhere to send anybody for the privacy, security and
    /// documentation pages - they are routes of the web client, and a development build has none. The
    /// rows are left out rather than offered dead. The licence is the exception: it is a file in the
    /// repository, at the same address for every deployment.
    /// </summary>
    [Fact]
    public void A_build_with_no_web_address_lists_the_licence_and_nothing_else()
    {
        using var localStore = new LocalStore();
        var about = Open(UnlockedPermissions.For(localStore), OrbitDocumentLinks.NoneButTheLicence);

        var only = Assert.Single(about.Documents);
        Assert.Contains("LICENSE", only.Url);
    }

    /// <summary>
    /// And a build that knows where its web client is lists all five, each under that client's own
    /// origin - the same pages Orbit.Web's footer links to, so there is one copy of each document.
    /// </summary>
    [Fact]
    public void A_deployed_build_lists_every_document_under_the_web_client()
    {
        using var localStore = new LocalStore();
        var about = Open(
            UnlockedPermissions.For(localStore),
            OrbitDocumentLinks.Under(new Uri("https://orbit-web.example/")));

        Assert.Equal(5, about.Documents.Count);
        Assert.Contains(about.Documents, document => document.Url == "https://orbit-web.example/privacy");
        Assert.Contains(about.Documents, document => document.Url == "https://orbit-web.example/security");
        Assert.Contains(about.Documents, document => document.Url == "https://orbit-web.example/docs");
    }

    private static AboutViewModel Open(UserPermissions permissions, OrbitDocumentLinks? documents = null)
        => new(
            new Translations(new InMemoryLanguageStore()),
            // Unreachable on purpose: the screen asks for the server's version and leaves it unsaid
            // when nobody answers, which is the ordinary case on a phone.
            new ServerVersionClient(StubHttpMessageHandler.Unreachable().ToHttpClient()),
            permissions,
            documents ?? OrbitDocumentLinks.NoneButTheLicence);
}
