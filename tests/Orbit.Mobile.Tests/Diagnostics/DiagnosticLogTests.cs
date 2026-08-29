using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Diagnostics;
using Xunit;

namespace Orbit.Mobile.Tests.Diagnostics;

/// <summary>
/// The log on the phone. Two things have to hold: the server's parser must be able to read it back, and
/// nothing Orbit promised to keep unreadable may end up in it - a log is a channel nobody thinks of as
/// data, which is exactly why it is worth guarding.
/// </summary>
public sealed class DiagnosticLogTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-27T11:20:31Z");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"orbit-log-{Guid.NewGuid():N}");
    private readonly FakeTimeProvider _clock = new(Now);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void An_entry_is_written_in_the_shape_the_server_parses()
    {
        // Orbit.Core's DiagnosticLogParser expects "[timestamp] [Level] message" and treats anything else
        // as a continuation of the line above. Getting this wrong loses the whole upload silently.
        var log = Build();

        log.Append("Warning", "Something went wrong");

        Assert.Equal($"[{Now.UtcDateTime:O}] [Warning] Something went wrong", log.ReadAll().Trim());
    }

    [Fact]
    public void Anything_shaped_like_key_material_never_reaches_the_file()
    {
        // The promise is that the server cannot read message content. A log that carried a key or a
        // ciphertext would hand it over through a channel nobody is watching.
        var log = Build();

        log.Append("Error", "Could not open BLIBbl+lm6CT5+EqNsm2a55oPfjwAIYZ3hre1kXCxsMKNIIFyJC2dwdTBiBBoufn");

        var written = log.ReadAll();
        Assert.Contains("[redacted]", written);
        Assert.DoesNotContain("BLIBbl", written);
    }

    [Fact]
    public void Ordinary_words_are_left_alone()
    {
        // The guard has to be worth having: redacting real messages would make the log useless.
        var log = Build();

        log.Append("Warning", "Could not reach the server while syncing notes");

        Assert.DoesNotContain("[redacted]", log.ReadAll());
    }

    [Fact]
    public void A_long_file_path_is_redacted_too_and_that_is_the_trade_it_makes()
    {
        // Not a false positive worth fixing: an iOS container path is long, and is made of the same
        // characters as base64. Over-redacting costs a path in a stack trace; under-redacting costs the
        // one promise Orbit makes about the server. This pins the choice so it stays a decision.
        var log = Build();

        log.Append("Error", "Could not open /var/mobile/Containers/Data/Application/Documents/orbit");

        Assert.Contains("[redacted]", log.ReadAll());
    }

    [Fact]
    public void A_full_file_rolls_over_and_the_older_one_is_still_readable()
    {
        // A crash mid-write leaves the current file truncated, and the previous one is what still makes
        // sense - which is the whole reason there are two.
        var log = Build();
        WriteUntilItRolls(log);

        log.Append("Error", "The line after the roll");

        var everything = log.ReadAll();
        Assert.Contains("The line after the roll", everything);
        Assert.Contains("filler", everything);
    }

    [Fact]
    public void The_log_stops_growing_rather_than_filling_the_phone()
    {
        var log = Build();

        for (var index = 0; index < 4000; index++)
        {
            log.Append("Warning", $"filler {index} {new string('x', 200)}");
        }

        // Two files, each capped: whatever the app does, this is the most it can ever take.
        Assert.True(log.ReadAll().Length <= DiagnosticLogFile.MaximumFileSizeBytes * 2);
    }

    [Fact]
    public void Clearing_leaves_nothing_behind()
    {
        var log = Build();
        log.Append("Warning", "Something");

        log.Clear();

        Assert.Equal(string.Empty, log.ReadAll());
    }

    [Fact]
    public void Nothing_below_the_chosen_level_is_recorded()
    {
        var log = Build();
        var verbosity = new DiagnosticLogVerbosity();
        var logger = new DiagnosticLogProvider(log, verbosity).CreateLogger("Orbit.Mobile.Sync.ChatSynchronizer");

        logger.LogDebug("Chatty");
        logger.LogWarning("Worth keeping");

        // Warnings and worse by default: a capped file filled with routine chatter loses the lines that
        // explain the failure.
        var written = log.ReadAll();
        Assert.DoesNotContain("Chatty", written);
        Assert.Contains("Worth keeping", written);
    }

    [Fact]
    public void Turning_verbosity_up_records_the_rest()
    {
        var log = Build();
        var verbosity = new DiagnosticLogVerbosity { IsVerbose = true };
        var logger = new DiagnosticLogProvider(log, verbosity).CreateLogger("Orbit.Mobile.Sync.ChatSynchronizer");

        logger.LogDebug("Chatty");

        Assert.Contains("Chatty", log.ReadAll());
    }

    [Fact]
    public void A_logged_exception_belongs_to_the_entry_above_it()
    {
        var log = Build();
        var logger = new DiagnosticLogProvider(log, new DiagnosticLogVerbosity())
            .CreateLogger("Orbit.Mobile.Sync.NoteSynchronizer");

        logger.LogError(new InvalidOperationException("boom"), "Could not sync");

        // The parser attaches lines that do not start an entry to the one before, so a stack trace has
        // to be on its own line rather than folded into the message.
        var lines = log.ReadAll().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("[", lines[0]);
        Assert.Contains("Could not sync", lines[0]);
        Assert.Contains("InvalidOperationException", lines[1]);
    }

    [Fact]
    public void An_entry_says_which_class_wrote_it_without_its_namespace()
    {
        var log = Build();
        var logger = new DiagnosticLogProvider(log, new DiagnosticLogVerbosity())
            .CreateLogger("Orbit.Mobile.Sync.ChatSynchronizer");

        logger.LogWarning("Something");

        Assert.Contains("ChatSynchronizer: Something", log.ReadAll());
        Assert.DoesNotContain("Orbit.Mobile.Sync.ChatSynchronizer", log.ReadAll());
    }

    private DiagnosticLogFile Build() => new(_directory, _clock);

    private static void WriteUntilItRolls(DiagnosticLogFile log)
    {
        var line = new string('x', 500);
        for (var index = 0; index < DiagnosticLogFile.MaximumFileSizeBytes / 500 + 10; index++)
        {
            log.Append("Warning", $"filler {index} {line}");
        }
    }
}
