namespace Orbit.Api.Diagnostics;

/// <summary>
/// How long uploaded mobile logs are kept. Diagnostic logs are the kind of data that accumulates forever
/// by default and is only ever read for a few days after it arrives, so retention is finite and stated
/// rather than left to grow - see info/orbit-maui-plan.md's "Diagnostic logs".
///
/// Swept hourly by DiagnosticLogRetentionBackgroundService, and again on every upload. Upload alone is
/// not enough: entries age whether or not anyone sends a new report, so a month with no reports left
/// the month before it sitting there untouched.
/// </summary>
public sealed class DiagnosticLogSettings
{
    public const string SectionName = "DiagnosticLogs";

    public int RetentionDays { get; set; } = 30;
}
