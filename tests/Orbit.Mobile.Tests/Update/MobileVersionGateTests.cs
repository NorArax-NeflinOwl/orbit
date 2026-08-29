using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Config;
using Orbit.Core.Mobile;
using Orbit.Mobile.Tests.TestDoubles;
using Orbit.Mobile.Update;
using Xunit;

namespace Orbit.Mobile.Tests.Update;

/// <summary>
/// The gate holds the splash screen, so getting it wrong is the most visible failure the app has: too
/// strict and it bricks itself off the network, too lax and a build the server cannot support keeps
/// running. The offline cases below are the ones the rule in info/orbit-maui-plan.md's "Forced update"
/// exists for, and are what these mostly pin down.
/// </summary>
public sealed class MobileVersionGateTests
{
    private const string CurrentVersion = "1.4.2";

    [Fact]
    public async Task An_update_the_server_demands_stops_the_app()
    {
        var context = new GateContext(StubHttpMessageHandler.RespondingWith(
            new MobileVersionVerdictDto("UpdateRequired", "2.0.0", "https://apps.apple.com/orbit")));

        var decision = await context.DecideAsync();

        Assert.True(decision.StopsTheApp);
        Assert.Equal("2.0.0", decision.LatestVersion);
        Assert.Equal("https://apps.apple.com/orbit", decision.UpdateUrl);
    }

    [Fact]
    public async Task An_update_the_server_merely_offers_does_not_stop_the_app()
    {
        var context = new GateContext(StubHttpMessageHandler.RespondingWith(
            new MobileVersionVerdictDto("UpdateAvailable", "2.0.0", "https://apps.apple.com/orbit")));

        var decision = await context.DecideAsync();

        Assert.False(decision.StopsTheApp);
        Assert.True(decision.OffersUpdate);
    }

    [Fact]
    public async Task The_app_asks_about_its_own_platform_and_version()
    {
        var context = new GateContext(StubHttpMessageHandler.RespondingWith(
            new MobileVersionVerdictDto("Supported", null, null)));

        await context.DecideAsync();

        var asked = Assert.Single(context.Handler.ReceivedRequests).Uri!.Query;
        Assert.Contains("platform=Ios", asked);
        Assert.Contains("version=1.4.2", asked);
    }

    [Fact]
    public async Task A_fresh_verdict_is_remembered_against_the_version_it_was_about()
    {
        var context = new GateContext(StubHttpMessageHandler.RespondingWith(
            new MobileVersionVerdictDto("UpdateRequired", "2.0.0", "https://apps.apple.com/orbit")));

        await context.DecideAsync();

        var remembered = Assert.IsType<CachedVersionVerdict>(context.Cache.Remembered);
        Assert.Equal(CurrentVersion, remembered.DisplayVersion);
        Assert.Equal(MobileVersionVerdict.UpdateRequired, remembered.Verdict);
    }

    [Fact]
    public async Task Offline_with_nothing_remembered_the_app_runs()
    {
        // The train. An app that blocked here would be broken by the very situation offline support
        // exists for, and this is the single most important behaviour in the gate.
        var context = new GateContext(StubHttpMessageHandler.Unreachable());

        var decision = await context.DecideAsync();

        Assert.False(decision.StopsTheApp);
    }

    [Fact]
    public async Task Offline_a_block_already_issued_for_this_version_still_stops_the_app()
    {
        var context = new GateContext(
            StubHttpMessageHandler.Unreachable(),
            remembered: new CachedVersionVerdict(
                CurrentVersion, MobileVersionVerdict.UpdateRequired, "2.0.0", "https://apps.apple.com/orbit"));

        var decision = await context.DecideAsync();

        // Going offline must not be a way around a block the app has already been told about.
        Assert.True(decision.StopsTheApp);
        Assert.Equal("https://apps.apple.com/orbit", decision.UpdateUrl);
    }

    [Fact]
    public async Task Offline_a_block_issued_for_an_older_version_does_not_stop_the_updated_app()
    {
        var context = new GateContext(
            StubHttpMessageHandler.Unreachable(),
            remembered: new CachedVersionVerdict(
                "1.0.0", MobileVersionVerdict.UpdateRequired, "2.0.0", "https://apps.apple.com/orbit"));

        var decision = await context.DecideAsync();

        // The user did exactly what they were told to do. Holding the old verdict against the new build
        // would leave them stuck with no way out at all.
        Assert.False(decision.StopsTheApp);
    }

    [Fact]
    public async Task A_verdict_this_build_cannot_read_falls_back_to_what_it_already_knows()
    {
        var context = new GateContext(
            StubHttpMessageHandler.RespondingWith(new MobileVersionVerdictDto("Quarantined", null, null)),
            remembered: new CachedVersionVerdict(CurrentVersion, MobileVersionVerdict.UpdateRequired, null, null));

        var decision = await context.DecideAsync();

        Assert.True(decision.StopsTheApp);
        // And it is not remembered as anything, since it was never understood.
        Assert.Equal(MobileVersionVerdict.UpdateRequired, context.Cache.Remembered!.Verdict);
    }

    [Fact]
    public async Task A_server_that_refuses_the_question_does_not_stop_the_app()
    {
        // An older deployment that has no version endpoint at all answers 404. That is not a verdict.
        var context = new GateContext(StubHttpMessageHandler.RespondingWith(HttpStatusCode.NotFound));

        var decision = await context.DecideAsync();

        Assert.False(decision.StopsTheApp);
    }

    [Fact]
    public async Task A_server_that_never_answers_releases_the_splash_screen_anyway()
    {
        var context = new GateContext(StubHttpMessageHandler.NeverAnswering());

        var decision = await context.DecideAsync().WaitAsync(MobileVersionGate.ServerTimeout * 4);

        Assert.False(decision.StopsTheApp);
    }

    [Fact]
    public async Task Shutting_down_mid_question_is_not_mistaken_for_an_offline_answer()
    {
        var context = new GateContext(StubHttpMessageHandler.NeverAnswering());
        using var shuttingDown = new CancellationTokenSource();
        await shuttingDown.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.DecideAsync(shuttingDown.Token));
    }

    private sealed class GateContext
    {
        public GateContext(StubHttpMessageHandler handler, CachedVersionVerdict? remembered = null)
        {
            Handler = handler;
            Cache = new InMemoryVersionVerdictCache(remembered);
            Gate = new MobileVersionGate(
                new AppVersion(MobilePlatform.Ios, CurrentVersion), handler.ToHttpClient(), Cache,
                NullLogger<MobileVersionGate>.Instance);
        }

        public StubHttpMessageHandler Handler { get; }
        public InMemoryVersionVerdictCache Cache { get; }
        public MobileVersionGate Gate { get; }

        public Task<VersionGateDecision> DecideAsync(CancellationToken cancellationToken = default)
            => Gate.DecideAsync(cancellationToken);
    }
}
