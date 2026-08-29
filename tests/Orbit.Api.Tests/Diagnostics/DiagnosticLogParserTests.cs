using Orbit.Core.Diagnostics;
using Xunit;

namespace Orbit.Api.Tests.Diagnostics;

/// <summary>
/// A log arrives from a phone that was already misbehaving, so the parser's job is to salvage what it
/// can rather than to validate. These pin down that it keeps the readable parts, attaches stack traces
/// to the entry they belong to, and cannot be used to flood the database from one request.
/// </summary>
public sealed class DiagnosticLogParserTests
{
    [Fact]
    public void An_entry_line_is_split_into_its_timestamp_level_and_message()
    {
        var entries = DiagnosticLogParser.Parse("[2026-08-26T11:20:31.1234567Z] [Warning] Failed to sync");

        var entry = Assert.Single(entries);
        Assert.Equal(DateTimeOffset.Parse("2026-08-26T11:20:31.1234567Z").ToUniversalTime(), entry.TimestampUtc);
        Assert.Equal("Warning", entry.Level);
        Assert.Equal("Failed to sync", entry.Message);
        Assert.Null(entry.Detail);
    }

    [Fact]
    public void A_stack_trace_stays_with_the_entry_it_belongs_to()
    {
        var log = """
            [2026-08-26T11:20:31.0000000Z] [Error] Upload failed
            System.Net.Http.HttpRequestException: Connection refused
               at Orbit.Maui.Sync.Outbox.FlushAsync()
            [2026-08-26T11:20:32.0000000Z] [Information] Retrying
            """;

        var entries = DiagnosticLogParser.Parse(log);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Upload failed", entries[0].Message);
        Assert.Contains("HttpRequestException", entries[0].Detail);
        Assert.Contains("Outbox.FlushAsync", entries[0].Detail);
        // The detail must not bleed into the next entry - that would attribute one failure to another.
        Assert.Equal("Retrying", entries[1].Message);
        Assert.Null(entries[1].Detail);
    }

    [Fact]
    public void Junk_before_the_first_entry_is_dropped_rather_than_kept_as_an_entry()
    {
        // A file truncated at the front is normal for a rolling log.
        var entries = DiagnosticLogParser.Parse("...trailing half of an older line\n[2026-08-26T11:20:31.0000000Z] [Debug] Started");

        var entry = Assert.Single(entries);
        Assert.Equal("Started", entry.Message);
    }

    [Fact]
    public void A_line_whose_timestamp_cannot_be_read_does_not_start_an_entry()
    {
        var log = """
            [2026-08-26T11:20:31.0000000Z] [Error] Real entry
            [not-a-timestamp] [Error] Looks like an entry but is not
            """;

        var entries = DiagnosticLogParser.Parse(log);

        // It reads as continuation of the entry above rather than being promoted to one of its own.
        var entry = Assert.Single(entries);
        Assert.Equal("Real entry", entry.Message);
        Assert.Contains("not-a-timestamp", entry.Detail);
    }

    [Fact]
    public void One_upload_cannot_store_more_entries_than_the_cap()
    {
        var log = string.Join('\n', Enumerable.Range(0, DiagnosticLogParser.MaximumEntriesPerUpload + 250)
            .Select(index => $"[2026-08-26T11:20:31.0000000Z] [Information] Entry {index}"));

        var entries = DiagnosticLogParser.Parse(log);

        Assert.Equal(DiagnosticLogParser.MaximumEntriesPerUpload, entries.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_file_parses_to_nothing_rather_than_failing(string? fileContent)
    {
        Assert.Empty(DiagnosticLogParser.Parse(fileContent));
    }

    [Fact]
    public void A_file_with_no_readable_entry_at_all_parses_to_nothing()
    {
        // The endpoint reports this back as "stored 0" rather than claiming the upload worked.
        Assert.Empty(DiagnosticLogParser.Parse("just\nsome\nplain text"));
    }

    [Fact]
    public void Windows_line_endings_do_not_end_up_inside_the_message()
    {
        var entries = DiagnosticLogParser.Parse("[2026-08-26T11:20:31.0000000Z] [Warning] Ends with CRLF\r\n");

        Assert.Equal("Ends with CRLF", Assert.Single(entries).Message);
    }

    [Fact]
    public void An_oversized_message_is_truncated_instead_of_failing_the_upload()
    {
        var entries = DiagnosticLogParser.Parse($"[2026-08-26T11:20:31.0000000Z] [Error] {new string('x', 5000)}");

        var entry = Assert.Single(entries);
        Assert.True(entry.Message.Length <= 1000, $"message was {entry.Message.Length} characters");
    }

    [Fact]
    public void A_local_timestamp_is_normalised_to_utc()
    {
        // Phones report their own offset; storing it as written would make two devices' logs
        // uncomparable, and Npgsql only accepts UTC for a timestamptz column anyway.
        var entries = DiagnosticLogParser.Parse("[2026-08-26T13:20:31.0000000+02:00] [Information] Local time");

        var entry = Assert.Single(entries);
        Assert.Equal(TimeSpan.Zero, entry.TimestampUtc.Offset);
        Assert.Equal(11, entry.TimestampUtc.Hour);
    }
}
