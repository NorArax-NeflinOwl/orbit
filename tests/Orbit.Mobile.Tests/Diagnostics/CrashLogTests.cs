using Microsoft.Extensions.Time.Testing;
using Orbit.Core.Diagnostics;
using Orbit.Mobile.Diagnostics;
using Xunit;

namespace Orbit.Mobile.Tests.Diagnostics;

/// <summary>
/// The crash that ends the app, written down before it does. Everything Orbit handles goes through
/// ILogger and reaches the log already; an unhandled exception ended the process, so the report a
/// reader sent afterwards said what was going on beforehand and nothing about what killed it.
///
/// Read back through the server's own parser rather than by looking at the text: what matters is not
/// that something was written but that the upload arrives as one readable entry carrying the stack.
/// </summary>
public sealed class CrashLogTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-29T11:20:31Z");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"orbit-crash-{Guid.NewGuid():N}");
    private readonly FakeTimeProvider _clock = new(Now);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void A_crash_arrives_at_the_server_as_one_entry_carrying_the_stack()
    {
        var log = new DiagnosticLogFile(_directory, _clock);

        new CrashLog(log).Record(Thrown(), "Android", isTerminating: true);

        var entry = Assert.Single(DiagnosticLogParser.Parse(log.ReadAll()));
        Assert.Equal("Critical", entry.Level);
        Assert.Contains("Orbit stopped", entry.Message);
        // The stack is the whole point of recording it, and it belongs to this entry rather than
        // becoming entries of its own - see DiagnosticLogParser.
        Assert.Contains("NullReferenceException", entry.Detail);
        Assert.Contains(nameof(Thrown), entry.Detail);
    }

    /// <summary>
    /// A task nobody awaited failing is the quieter case and the one worth having: the reader saw
    /// nothing at all, and whatever it stood for silently did not happen.
    /// </summary>
    [Fact]
    public void A_failure_the_app_survived_is_told_apart_from_the_one_that_ended_it()
    {
        var log = new DiagnosticLogFile(_directory, _clock);

        new CrashLog(log).Record(Thrown(), "an unwatched task", isTerminating: false);

        var entry = Assert.Single(DiagnosticLogParser.Parse(log.ReadAll()));
        Assert.DoesNotContain("Orbit stopped", entry.Message);
        Assert.Contains("an unwatched task", entry.Message);
    }

    /// <summary>
    /// A handler can be handed something that is not an Exception at all - the runtime's own hook types
    /// it as object. That it happened is still worth more than nothing, and must not itself throw.
    /// </summary>
    [Fact]
    public void A_crash_with_no_exception_to_show_is_still_recorded()
    {
        var log = new DiagnosticLogFile(_directory, _clock);

        new CrashLog(log).Record(failure: null, "the runtime", isTerminating: true);

        var entry = Assert.Single(DiagnosticLogParser.Parse(log.ReadAll()));
        Assert.Contains("Orbit stopped", entry.Message);
    }

    /// <summary>
    /// One crash is one entry. Both the platform's handler and the runtime's fire for the same
    /// exception on Android, and a report saying it happened twice invites somebody to look for a
    /// second failure that never happened.
    /// </summary>
    [Fact]
    public void The_same_failure_reaching_two_handlers_is_recorded_once()
    {
        var log = new DiagnosticLogFile(_directory, _clock);
        var crashLog = new CrashLog(log);
        var failure = Thrown();

        crashLog.Record(failure, "Android", isTerminating: true);
        crashLog.Record(failure, "the runtime", isTerminating: true);

        var entry = Assert.Single(DiagnosticLogParser.Parse(log.ReadAll()));
        Assert.Contains("Android", entry.Message);
    }

    /// <summary>Two failures are two crashes, however alike they look.</summary>
    [Fact]
    public void A_second_failure_is_recorded_in_its_own_right()
    {
        var log = new DiagnosticLogFile(_directory, _clock);
        var crashLog = new CrashLog(log);

        crashLog.Record(Thrown(), "an unwatched task", isTerminating: false);
        crashLog.Record(Thrown(), "an unwatched task", isTerminating: false);

        Assert.Equal(2, DiagnosticLogParser.Parse(log.ReadAll()).Count);
    }

    /// <summary>What was going on beforehand is what makes the crash readable, so it has to survive.</summary>
    [Fact]
    public void It_is_added_to_what_the_log_already_held()
    {
        var log = new DiagnosticLogFile(_directory, _clock);
        log.Append("Warning", "Could not reach Orbit");

        new CrashLog(log).Record(Thrown(), "Android", isTerminating: true);

        var entries = DiagnosticLogParser.Parse(log.ReadAll());
        Assert.Equal(2, entries.Count);
        Assert.Contains("Could not reach Orbit", entries[0].Message);
    }

    /// <summary>A real exception, thrown so it carries a stack - a constructed one has none.</summary>
    private static Exception Thrown()
    {
        try
        {
            throw new NullReferenceException("Object reference not set to an instance of an object.");
        }
        catch (NullReferenceException caught)
        {
            return caught;
        }
    }
}
