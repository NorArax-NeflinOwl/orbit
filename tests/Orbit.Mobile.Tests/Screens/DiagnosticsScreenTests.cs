using System.Net;
using Microsoft.Extensions.Time.Testing;
using Orbit.Core.Mobile;
using Orbit.Mobile.Api;
using Orbit.Mobile.Diagnostics;
using Orbit.Mobile.Screens.Diagnostics;
using Orbit.Mobile.Tests.TestDoubles;
using Orbit.Mobile.Update;
using Xunit;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The screen that sends a log. Its whole subject is that sending is a decision: the reader sees what
/// would go, and nothing leaves the phone until they press the button.
/// </summary>
public sealed class DiagnosticsScreenTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"orbit-diag-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void An_empty_log_says_so_rather_than_looking_broken()
    {
        var context = new DiagnosticsContext(_directory);
        var screen = context.Open();

        screen.LoadCommand.Execute(null);

        Assert.True(screen.HasNothing);
    }

    [Fact]
    public async Task Nothing_is_sent_before_the_button_is_pressed()
    {
        // The rule the whole feature rests on: no log leaves the phone on its own.
        var context = new DiagnosticsContext(_directory);
        context.Log.Append("Warning", "Something happened");
        var screen = context.Open();

        screen.LoadCommand.Execute(null);

        Assert.Empty(context.Server.Uploads);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Sending_carries_the_log_and_what_produced_it()
    {
        var context = new DiagnosticsContext(_directory);
        context.Log.Append("Error", "Could not sync");
        var screen = context.Open();

        await screen.SendCommand.ExecuteAsync(null);

        var upload = Assert.Single(context.Server.Uploads);
        Assert.Contains("Could not sync", upload.FileContent);
        // Without these a report says "it crashed" rather than "it crashes on that iOS version".
        Assert.Equal("Ios", upload.Platform);
        Assert.Equal("18.0", upload.OperatingSystemVersion);
        Assert.Equal("iPhone17,1", upload.DeviceModel);
        Assert.Equal("0.1.0", upload.AppVersion);
    }

    [Fact]
    public async Task An_empty_log_is_not_sent_at_all()
    {
        var context = new DiagnosticsContext(_directory);
        var screen = context.Open();

        await screen.SendCommand.ExecuteAsync(null);

        Assert.Empty(context.Server.Uploads);
        Assert.Contains("nothing to send", screen.Message);
    }

    [Fact]
    public async Task A_log_the_server_could_not_read_is_not_reported_as_a_success()
    {
        // It arrived and told the server nothing, which is a different thing from having been sent.
        var context = new DiagnosticsContext(_directory);
        context.Log.Append("Warning", "Something");
        context.Server.StoredEntryCount = 0;
        var screen = context.Open();

        await screen.SendCommand.ExecuteAsync(null);

        Assert.Contains("nothing in the log could be read", screen.Message);
    }

    [Fact]
    public async Task Being_out_of_reach_says_so()
    {
        var context = new DiagnosticsContext(_directory);
        context.Log.Append("Warning", "Something");
        context.Server.IsUnreachable = true;
        var screen = context.Open();

        await screen.SendCommand.ExecuteAsync(null);

        Assert.Contains("out of reach", screen.Message);
    }

    [Fact]
    public async Task A_refusal_is_not_reported_as_being_offline()
    {
        var context = new DiagnosticsContext(_directory);
        context.Log.Append("Warning", "Something");
        context.Server.RefuseEverythingWith = HttpStatusCode.Unauthorized;
        var screen = context.Open();

        await screen.SendCommand.ExecuteAsync(null);

        Assert.DoesNotContain("out of reach", screen.Message);
        Assert.Contains("signing in", screen.Message);
    }

    [Fact]
    public void Clearing_throws_the_log_away()
    {
        // The log is the reader's own record of their own device; somebody who has decided not to send
        // it should be able to be rid of it.
        var context = new DiagnosticsContext(_directory);
        context.Log.Append("Warning", "Something");
        var screen = context.Open();
        screen.LoadCommand.Execute(null);

        screen.ClearCommand.Execute(null);

        Assert.True(screen.HasNothing);
        Assert.Equal(string.Empty, context.Log.ReadAll());
    }

    private sealed class DiagnosticsContext
    {
        public DiagnosticsContext(string directory)
            => Log = new DiagnosticLogFile(directory, new FakeTimeProvider(DateTimeOffset.Parse("2026-08-27T11:00:00Z")));

        public DiagnosticLogFile Log { get; }

        public FakeDiagnosticsServer Server { get; } = new();

        public RecordingScreenNavigator Navigator { get; } = new();

        public DiagnosticsViewModel Open()
            => new(
                Log, new DiagnosticLogVerbosity(), new DiagnosticsClient(Server.ToHttpClient()),
                new FixedDeviceDescription(), new AppVersion(MobilePlatform.Ios, "0.1.0"),
                new Translations(new InMemoryLanguageStore()), Navigator);
    }
}
