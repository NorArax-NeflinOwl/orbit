using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Covers the settings that belong to this browser rather than the account. The defaults matter most:
/// they are what someone gets before they have said anything, and one of them decides whether the
/// browser is allowed to ask where they are.
/// </summary>
public sealed class DevicePreferencesTests
{
    [Fact]
    public async Task Location_is_off_until_someone_says_otherwise()
    {
        var preferences = new DevicePreferences(new RecordingJSRuntime(), NullLogger<DevicePreferences>.Instance);

        await preferences.InitializeAsync();

        // The browser's own permission prompt is a question worth having agreed to beforehand.
        Assert.False(preferences.AllowLocation);
    }

    [Fact]
    public async Task The_log_keeps_warnings_and_worse_by_default()
    {
        var preferences = new DevicePreferences(new RecordingJSRuntime(), NullLogger<DevicePreferences>.Instance);

        await preferences.InitializeAsync();

        Assert.Equal(LogLevel.Warning, preferences.MinimumLogLevel);
    }

    [Fact]
    public async Task Diagnostics_start_out_of_the_way()
    {
        var preferences = new DevicePreferences(new RecordingJSRuntime(), NullLogger<DevicePreferences>.Instance);

        await preferences.InitializeAsync();

        Assert.Equal(DiagnosticsMode.Release, preferences.DiagnosticsMode);
    }

    [Fact]
    public async Task What_was_stored_is_read_back()
    {
        var jsRuntime = new RecordingJSRuntime
        {
            Stored =
            {
                ["orbit-allow-location"] = "true",
                ["orbit-diagnostics-mode"] = "Debug",
                ["orbit-minimum-log-level"] = "Trace"
            }
        };
        var preferences = new DevicePreferences(jsRuntime, NullLogger<DevicePreferences>.Instance);

        await preferences.InitializeAsync();

        Assert.True(preferences.AllowLocation);
        Assert.Equal(DiagnosticsMode.Debug, preferences.DiagnosticsMode);
        Assert.Equal(LogLevel.Trace, preferences.MinimumLogLevel);
    }

    [Fact]
    public async Task Nonsense_in_storage_reads_as_the_default()
    {
        // Storage is the browser's, and anything could be in it - an unreadable value is one this build
        // doesn't understand, and the safe reading of that is the default.
        var jsRuntime = new RecordingJSRuntime
        {
            Stored = { ["orbit-minimum-log-level"] = "Chatty", ["orbit-diagnostics-mode"] = "Whatever" }
        };
        var preferences = new DevicePreferences(jsRuntime, NullLogger<DevicePreferences>.Instance);

        await preferences.InitializeAsync();

        Assert.Equal(LogLevel.Warning, preferences.MinimumLogLevel);
        Assert.Equal(DiagnosticsMode.Release, preferences.DiagnosticsMode);
    }

    [Fact]
    public async Task Setting_something_writes_it_and_announces_it()
    {
        var jsRuntime = new RecordingJSRuntime();
        var preferences = new DevicePreferences(jsRuntime, NullLogger<DevicePreferences>.Instance);
        var announced = 0;
        preferences.Changed += () => announced++;

        await preferences.SetAllowLocationAsync(true);

        Assert.True(preferences.AllowLocation);
        Assert.Equal("true", jsRuntime.Stored["orbit-allow-location"]);
        Assert.Equal(1, announced);
    }

    [Fact]
    public async Task A_browser_that_blocks_storage_still_loads()
    {
        // Private windows and embedded webviews throw on localStorage outright. The page has to open
        // anyway, and the defaults are the right answer - "don't ask for the location" the safe way round.
        var preferences = new DevicePreferences(new ThrowingJSRuntime(), NullLogger<DevicePreferences>.Instance);

        await preferences.InitializeAsync();

        Assert.False(preferences.AllowLocation);
        Assert.Equal(LogLevel.Warning, preferences.MinimumLogLevel);
    }

    [Fact]
    public async Task A_setting_still_applies_for_this_session_when_it_cannot_be_stored()
    {
        var preferences = new DevicePreferences(new ThrowingJSRuntime(), NullLogger<DevicePreferences>.Instance);

        await preferences.SetAllowLocationAsync(true);

        // It just won't be remembered next time.
        Assert.True(preferences.AllowLocation);
    }

    private sealed class RecordingJSRuntime : IJSRuntime
    {
        public Dictionary<string, string> Stored { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "localStorage.getItem")
            {
                var key = args![0]!.ToString()!;
                return ValueTask.FromResult((TValue)(object?)Stored.GetValueOrDefault(key)!);
            }

            if (identifier == "localStorage.setItem")
            {
                Stored[args![0]!.ToString()!] = args[1]!.ToString()!;
            }

            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }

    private sealed class ThrowingJSRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new JSException("The browser refuses to store anything.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => throw new JSException("The browser refuses to store anything.");
    }
}
