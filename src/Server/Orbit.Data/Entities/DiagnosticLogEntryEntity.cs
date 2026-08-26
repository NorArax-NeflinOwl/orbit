namespace Orbit.Data.Entities;

/// <summary>
/// One parsed line from a mobile log upload (see Orbit.Core.Diagnostics.DiagnosticLogEntry), with the
/// device information from the upload it arrived in repeated on every row. Denormalised on purpose: a
/// report is always read as "these entries, from this device", and a separate upload table would mean a
/// join on every read to recover the half that identifies the bug.
/// </summary>
public sealed class DiagnosticLogEntryEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>When the server received the upload - what retention is measured against.</summary>
    public DateTimeOffset ReceivedAtUtc { get; set; }

    /// <summary>When the entry was written on the device, which can be much earlier, and offline.</summary>
    public DateTimeOffset TimestampUtc { get; set; }

    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>Whatever followed the entry line - usually a stack trace.</summary>
    public string? Detail { get; set; }

    public string AppVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string OperatingSystemVersion { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
}
