using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Config;
using Orbit.Core.Mobile;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Update;
using Orbit.Mobile.Tests.TestDoubles;
using Orbit.Mobile.Update;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Where a newer Orbit comes from - the phone's answer to Orbit.Web's "Get the app" page.
///
/// Two things make it a different screen. It shows one platform, its own, because a phone knows what it
/// is and the other half would only be read past; and it says where the reader stands, because whoever
/// opens it already has the app. It asks the server as it opens, since opening it is the question - and
/// falls back to what the last answer was when it cannot reach anybody, so it still says something with
/// no connection.
/// </summary>
public sealed class UpdateScreenTests
{
    private const string Installed = "1.3.0";

    [Fact]
    public async Task On_android_only_the_android_half_is_drawn()
    {
        var screen = Open(MobilePlatform.Android);

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.IsAndroid);
        Assert.False(screen.IsIphone);
    }

    [Fact]
    public async Task On_an_iphone_only_the_iphone_half_is_drawn()
    {
        var screen = Open(MobilePlatform.Ios);

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.IsIphone);
        Assert.False(screen.IsAndroid);
    }

    [Fact]
    public async Task A_newer_build_is_named_beside_the_one_installed()
    {
        var screen = Open(MobilePlatform.Android, ANewerOne());

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Contains("1.4.0", screen.Summary);
        Assert.Contains(Installed, screen.Summary);
        Assert.True(screen.CanUpdate);
    }

    [Fact]
    public async Task The_newest_build_says_so_and_offers_nothing()
    {
        var screen = Open(
            MobilePlatform.Android,
            new CachedVersionVerdict(Installed, MobileVersionVerdict.Supported, Installed, null));

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Contains(Installed, screen.Summary);
        Assert.False(screen.CanUpdate);
    }

    /// <summary>
    /// Never having reached the server is not the same as having checked and found nothing, and a screen
    /// that said "you have the newest" would be making a claim rather than giving an answer.
    /// </summary>
    [Fact]
    public async Task Having_never_checked_is_said_out_loud()
    {
        var screen = Open(MobilePlatform.Android);

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(screen.Summary);
        Assert.False(screen.CanUpdate);
    }

    /// <summary>
    /// A verdict about a build that has since been replaced says nothing about this one - which is the
    /// rule the version gate already applies, and the reason this screen asks it rather than the cache.
    /// </summary>
    [Fact]
    public async Task A_verdict_about_an_older_build_is_not_applied_to_this_one()
    {
        var screen = Open(
            MobilePlatform.Android,
            new CachedVersionVerdict("1.0.0", MobileVersionVerdict.UpdateAvailable, "1.4.0", "https://orbit.example/apk"));

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.DoesNotContain("1.4.0", screen.Summary);
        Assert.False(screen.CanUpdate);
    }

    [Fact]
    public async Task Getting_it_leaves_orbit_for_where_the_build_is()
    {
        var link = new RecordingUpdateLink();
        var screen = Open(MobilePlatform.Android, ANewerOne(), link);
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.GetItCommand.ExecuteAsync(null);

        Assert.Equal("https://orbit.example/apk", link.Opened);
    }

    /// <summary>Nothing to point at is nowhere to send anybody, and tapping must not pretend otherwise.</summary>
    [Fact]
    public async Task With_nowhere_to_send_anybody_nothing_is_opened()
    {
        var link = new RecordingUpdateLink();
        var screen = Open(MobilePlatform.Android, link: link);
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.GetItCommand.ExecuteAsync(null);

        Assert.Null(link.Opened);
    }

    /// <summary>
    /// Nothing to fetch, nothing to head. Everything under the platform heading is about fetching a
    /// build, so with none on offer the heading stood alone over an empty space - which reads as a
    /// screen still loading rather than one that has answered. Found on a device.
    /// </summary>
    [Fact]
    public async Task With_nothing_to_fetch_the_platform_heading_is_not_drawn_either()
    {
        var screen = Open(MobilePlatform.Android);

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(screen.CanUpdate);
        Assert.False(screen.HasAndroidBuild);
        // Still an Android phone - it just has nothing to download.
        Assert.True(screen.IsAndroid);
    }

    [Fact]
    public async Task A_build_on_offer_brings_its_platform_heading_with_it()
    {
        var screen = Open(MobilePlatform.Android, ANewerOne());

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.HasAndroidBuild);
        Assert.False(screen.HasIphoneBuild);
    }

    [Fact]
    public async Task An_iphone_build_on_offer_brings_the_other_heading()
    {
        var screen = Open(MobilePlatform.Ios, ANewerOne());

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.HasIphoneBuild);
        Assert.False(screen.HasAndroidBuild);
    }

    /// <summary>
    /// Opening this screen is the question "is there a newer one", so it asks the server rather than
    /// repeating what startup was told. Until 2026-09-24 it read the remembered verdict alone, so a
    /// release published while the app was running was invisible here however often the screen was
    /// opened - reported as "checking for an update does not find the newest version".
    /// </summary>
    [Fact]
    public async Task A_release_published_since_this_app_started_is_found()
    {
        // What the last launch was told, and what has happened since.
        var screen = Open(
            MobilePlatform.Android,
            new CachedVersionVerdict(Installed, MobileVersionVerdict.Supported, Installed, null),
            server: StubHttpMessageHandler.RespondingWith(
                new MobileVersionVerdictDto(
                    nameof(MobileVersionVerdict.UpdateAvailable), "1.4.0", "https://orbit.example/apk")));

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Contains("1.4.0", screen.Summary);
        Assert.True(screen.CanUpdate);
    }

    /// <summary>
    /// And what it is told is remembered, so the next launch has an answer without asking - which is
    /// what lets the gate decide at all on a phone with no signal.
    /// </summary>
    [Fact]
    public async Task And_what_the_server_said_is_remembered()
    {
        var cache = new InMemoryVersionVerdictCache(null);
        var appVersion = new AppVersion(MobilePlatform.Android, Installed);
        var gate = new MobileVersionGate(
            appVersion,
            StubHttpMessageHandler.RespondingWith(
                    new MobileVersionVerdictDto(
                        nameof(MobileVersionVerdict.UpdateAvailable), "1.4.0", "https://orbit.example/apk"))
                .ToHttpClient(),
            cache,
            NullLogger<MobileVersionGate>.Instance);
        var screen = new UpdateViewModel(
            gate, appVersion, new RecordingUpdateLink(), new Translations(new InMemoryLanguageStore()));

        await screen.LoadCommand.ExecuteAsync(null);

        var remembered = await cache.ReadAsync(CancellationToken.None);
        Assert.Equal("1.4.0", remembered!.LatestVersion);
        Assert.Equal(Installed, remembered.DisplayVersion);
    }

    private static CachedVersionVerdict ANewerOne()
        => new(Installed, MobileVersionVerdict.UpdateAvailable, "1.4.0", "https://orbit.example/apk");

    private static UpdateViewModel Open(
        MobilePlatform platform, CachedVersionVerdict? remembered = null, IUpdateLink? link = null,
        StubHttpMessageHandler? server = null)
    {
        var appVersion = new AppVersion(platform, Installed);
        var gate = new MobileVersionGate(
            appVersion,
            // Unreachable unless a test says otherwise, which is what puts every one of the tests above
            // on the remembered-verdict path - the answer a phone with no signal gets.
            (server ?? StubHttpMessageHandler.Unreachable()).ToHttpClient(),
            new InMemoryVersionVerdictCache(remembered),
            NullLogger<MobileVersionGate>.Instance);

        return new UpdateViewModel(
            gate, appVersion, link ?? new RecordingUpdateLink(), new Translations(new InMemoryLanguageStore()));
    }

    /// <summary>Leaving the app is a platform call, so a test only checks where it was asked to go.</summary>
    private sealed class RecordingUpdateLink : IUpdateLink
    {
        public string? Opened { get; private set; }

        public Task OpenAsync(string url)
        {
            Opened = url;
            return Task.CompletedTask;
        }
    }
}
