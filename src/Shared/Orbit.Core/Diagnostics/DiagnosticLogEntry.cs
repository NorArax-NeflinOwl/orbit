namespace Orbit.Core.Diagnostics;

/// <summary>
/// One line of a mobile log file, after <see cref="DiagnosticLogParser"/> has read it. Detail holds
/// whatever followed the line - a stack trace, usually - which is the part that makes a report
/// actionable and the part most likely to be long.
/// </summary>
public sealed record DiagnosticLogEntry(DateTimeOffset TimestampUtc, string Level, string Message, string? Detail);
