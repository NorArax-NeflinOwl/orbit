using Microsoft.Extensions.Logging.Abstractions;
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
/// opens it already has the app. It asks nobody: what is on offer is the verdict startup obtained, so
/// this works with no connection and costs nothing to open.
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

    private static CachedVersionVerdict ANewerOne()
        => new(Installed, MobileVersionVerdict.UpdateAvailable, "1.4.0", "https://orbit.example/apk");

    private static UpdateViewModel Open(
        MobilePlatform platform, CachedVersionVerdict? remembered = null, IUpdateLink? link = null)
    {
        var appVersion = new AppVersion(platform, Installed);
        var gate = new MobileVersionGate(
            appVersion,
            // Unreachable on purpose: this screen reads what is remembered and asks nobody.
            StubHttpMessageHandler.Unreachable().ToHttpClient(),
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
