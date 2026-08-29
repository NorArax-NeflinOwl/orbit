namespace Orbit.Mobile.Diagnostics;

/// <summary>
/// The last thing the app writes down.
///
/// A crash was the one failure nothing recorded. Everything the app handles goes through ILogger and
/// into <see cref="DiagnosticLogFile"/>, so a report sent from the diagnostics screen explains most
/// things - but an unhandled exception ends the process, and the log then held only what was going on
/// beforehand, with the exception that killed the app missing entirely. A reader watching their phone
/// close itself had nothing to send that said why.
///
/// Written straight to the file rather than through ILogger. The process is about to die: there is no
/// room for a queue, a background flush, or a container that is still alive to resolve anything from.
/// </summary>
public sealed class CrashLog
{
    private readonly DiagnosticLogFile _file;

    /// <summary>
    /// The last failure written down, so one crash is one entry. Both the platform's handler and the
    /// runtime's fire for the same exception on Android - the two are not alternatives, and neither
    /// covers every path, so both are watched and the second sighting is dropped rather than the hook.
    /// </summary>
    private Exception? _alreadyRecorded;

    public CrashLog(DiagnosticLogFile file) => _file = file;

    /// <summary>
    /// Records a failure nobody caught, in the shape the server's DiagnosticLogParser reads: one line
    /// naming what happened, and the exception beneath it as the entry's detail.
    /// </summary>
    /// <param name="source">
    /// Which way it arrived - the platform's own handler, the runtime's, or an unwatched task. Worth
    /// keeping: the same exception reaching a different handler says something about which thread it
    /// was on and whether anything had a chance to stop it.
    /// </param>
    /// <param name="isTerminating">
    /// Whether the app is going down with it. False is the quieter and more useful case: a task nobody
    /// awaited failed, the reader saw nothing at all, and the work it stood for silently did not happen.
    /// </param>
    public void Record(Exception? failure, string source, bool isTerminating)
    {
        try
        {
            if (failure is not null && ReferenceEquals(failure, _alreadyRecorded))
            {
                return;
            }

            _alreadyRecorded = failure;
            var what = isTerminating ? "Orbit stopped" : "Something failed with nobody watching";
            var message = $"{what} ({source}).";
            if (failure is not null)
            {
                // On its own line, which is what makes it a continuation the server's parser attaches
                // to this entry rather than a new one - see DiagnosticLogProvider.
                message += Environment.NewLine + failure;
            }

            _file.Append("Critical", message);
        }
        catch (Exception writingFailed) when (writingFailed is IOException or UnauthorizedAccessException)
        {
            // A crash handler that throws while handling a crash replaces one failure with a worse one:
            // the reader loses the app either way, and this way loses the log as well.
        }
    }
}
