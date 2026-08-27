using System.Text;
using System.Text.RegularExpressions;

namespace Orbit.Mobile.Diagnostics;

/// <summary>
/// The app's own log, on the phone, in the shape the server's DiagnosticLogParser reads:
///
/// <code>[2026-08-26T11:20:31.1234567Z] [Warning] Something went wrong</code>
///
/// Two files rather than one, and a size cap on each. A log has to survive the thing it is meant to
/// explain - a crash mid-write leaves the current file truncated, and the previous one is what still
/// makes sense - while never growing without bound on somebody's phone. Two is the smallest count that
/// gets both.
/// </summary>
public sealed partial class DiagnosticLogFile
{
    /// <summary>
    /// Roughly a few thousand lines. Large enough to hold the run-up to a failure, small enough that
    /// sending it is not a considerate thing to ask of somebody's data plan.
    /// </summary>
    public const long MaximumFileSizeBytes = 256 * 1024;

    private const string CurrentFileName = "orbit.log";
    private const string PreviousFileName = "orbit.previous.log";

    /// <summary>
    /// A backstop, not a guarantee. Orbit's promise is that the server cannot read message content, and
    /// a log is a channel nobody thinks of as data - so anything shaped like key material or ciphertext
    /// is replaced before it ever reaches the file. The real defence is not logging such things in the
    /// first place, which is a review rule for every log statement added later (see the plan's §8); this
    /// catches the one that slips through.
    /// </summary>
    [GeneratedRegex(@"[A-Za-z0-9+/_-]{40,}={0,2}")]
    private static partial Regex SomethingSecretShaped();

    private readonly Lock _gate = new();
    private readonly string _directory;
    private readonly TimeProvider _timeProvider;

    public DiagnosticLogFile(string directory, TimeProvider timeProvider)
    {
        _directory = directory;
        _timeProvider = timeProvider;
    }

    private string CurrentPath => Path.Combine(_directory, CurrentFileName);

    private string PreviousPath => Path.Combine(_directory, PreviousFileName);

    public void Append(string level, string message)
    {
        var line = $"[{_timeProvider.GetUtcNow().UtcDateTime:O}] [{level}] {Redact(message)}";

        lock (_gate)
        {
            Directory.CreateDirectory(_directory);
            RollIfFull();
            File.AppendAllText(CurrentPath, line + Environment.NewLine, Encoding.UTF8);
        }
    }

    /// <summary>
    /// Everything held, oldest first, ready to upload. Empty when nothing has been written, which the
    /// caller reports rather than sending a blank file.
    /// </summary>
    public string ReadAll()
    {
        lock (_gate)
        {
            var builder = new StringBuilder();
            foreach (var path in new[] { PreviousPath, CurrentPath })
            {
                if (File.Exists(path))
                {
                    builder.Append(File.ReadAllText(path));
                }
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Throws it all away. Offered because a log is the reader's own record of their own device, and
    /// somebody who has decided not to send it should be able to be rid of it.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            File.Delete(CurrentPath);
            File.Delete(PreviousPath);
        }
    }

    /// <summary>
    /// The current file becomes the previous one, and whatever was previous is gone. Called while
    /// holding the lock.
    /// </summary>
    private void RollIfFull()
    {
        if (!File.Exists(CurrentPath) || new FileInfo(CurrentPath).Length < MaximumFileSizeBytes)
        {
            return;
        }

        File.Delete(PreviousPath);
        File.Move(CurrentPath, PreviousPath);
    }

    private static string Redact(string message)
        => SomethingSecretShaped().Replace(message, "[redacted]");
}
