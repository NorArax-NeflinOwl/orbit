namespace Orbit.Api.Diagnostics;

/// <summary>
/// How long uploaded mobile logs are kept. Diagnostic logs are the kind of data that accumulates forever
/// by default and is only ever read for a few days after it arrives, so retention is finite and stated
/// rather than left to grow - see info/orbit-maui-plan.md's "Diagnostic logs".
///
/// Enforced on upload (see DiagnosticLogEndpoints), which is the only time entries appear and therefore
/// the only time there is anything to sweep.
/// </summary>
public sealed class DiagnosticLogSettings
{
    public const string SectionName = "DiagnosticLogs";

    public int RetentionDays { get; set; } = 30;
}
