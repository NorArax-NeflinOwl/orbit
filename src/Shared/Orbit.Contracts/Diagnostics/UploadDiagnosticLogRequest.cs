namespace Orbit.Contracts.Diagnostics;

/// <summary>
/// A mobile log file, sent whole, with the device that produced it. The server parses the file rather
/// than trusting the app to have structured it - see Orbit.Core's DiagnosticLogParser for the line shape
/// it expects.
///
/// Sending is always something the user chose to do from the options screen. Nothing here is collected
/// or uploaded on its own.
/// </summary>
/// <param name="FileContent">The log file's text, as written on the device.</param>
public sealed record UploadDiagnosticLogRequest(
    string FileContent, string AppVersion, string Platform, string OperatingSystemVersion, string DeviceModel);

/// <summary>How much of the upload was actually stored, so the app can tell the user it arrived.</summary>
public sealed record UploadDiagnosticLogResponse(int StoredEntryCount);
