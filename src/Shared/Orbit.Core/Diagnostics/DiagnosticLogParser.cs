using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Orbit.Core.Diagnostics;

/// <summary>
/// Reads an uploaded mobile log file into entries. The app writes one line per entry in a fixed shape:
///
/// <code>[2026-08-26T11:20:31.1234567Z] [Warning] Something went wrong</code>
///
/// and anything that doesn't start that way - a stack trace, a wrapped message - belongs to the entry
/// above it. Real log files look like this anyway, so a person reading the raw file and the server
/// reading it see the same thing.
///
/// The parser is deliberately forgiving. A log arrives from a phone that was already misbehaving, often
/// truncated mid-write, so anything unreadable is skipped rather than failing the upload: a partly
/// readable report is worth keeping, and refusing it would lose the entries that explain the crash.
/// </summary>
public static partial class DiagnosticLogParser
{
    /// <summary>Caps one upload, so a runaway log can't be used to fill the database in a single request.</summary>
    public const int MaximumEntriesPerUpload = 500;

    private const int MaximumMessageLength = 1000;
    private const int MaximumDetailLength = 4000;

    public static IReadOnlyList<DiagnosticLogEntry> Parse(string? fileContent)
    {
        if (string.IsNullOrWhiteSpace(fileContent))
        {
            return [];
        }

        var entries = new List<DiagnosticLogEntry>();
        ParsedEntryBuilder? current = null;

        foreach (var line in fileContent.Split('\n'))
        {
            var match = EntryLine().Match(line.TrimEnd('\r'));
            if (!match.Success)
            {
                // A continuation of the entry above - or leading junk, if nothing has started yet.
                current?.AppendDetail(line.TrimEnd('\r'));
                continue;
            }

            if (!DateTimeOffset.TryParse(
                    match.Groups["timestamp"].Value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var timestamp))
            {
                current?.AppendDetail(line.TrimEnd('\r'));
                continue;
            }

            Complete(entries, current);
            if (entries.Count >= MaximumEntriesPerUpload)
            {
                return entries;
            }

            current = new ParsedEntryBuilder(timestamp, match.Groups["level"].Value, match.Groups["message"].Value);
        }

        Complete(entries, current);
        return entries;
    }

    private static void Complete(List<DiagnosticLogEntry> entries, ParsedEntryBuilder? builder)
    {
        if (builder is not null && entries.Count < MaximumEntriesPerUpload)
        {
            entries.Add(builder.Build());
        }
    }

    [GeneratedRegex(@"^\[(?<timestamp>[^\]]+)\]\s*\[(?<level>[^\]]{1,20})\]\s?(?<message>.*)$", RegexOptions.Compiled)]
    private static partial Regex EntryLine();

    /// <summary>
    /// Accumulates the continuation lines that follow an entry before it can be built, so the detail is
    /// assembled once rather than by repeatedly concatenating a growing string.
    /// </summary>
    private sealed class ParsedEntryBuilder(DateTimeOffset timestampUtc, string level, string message)
    {
        private readonly StringBuilder _detail = new();

        public void AppendDetail(string line)
        {
            if (_detail.Length >= MaximumDetailLength || string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (_detail.Length > 0)
            {
                _detail.Append('\n');
            }

            _detail.Append(line);
        }

        public DiagnosticLogEntry Build() => new(
            timestampUtc,
            Limit(level, 20),
            Limit(message, MaximumMessageLength),
            _detail.Length == 0 ? null : Limit(_detail.ToString(), MaximumDetailLength));

        private static string Limit(string value, int maximumLength)
        {
            var trimmed = value.Trim();
            return trimmed.Length <= maximumLength ? trimmed : trimmed[..maximumLength];
        }
    }
}
