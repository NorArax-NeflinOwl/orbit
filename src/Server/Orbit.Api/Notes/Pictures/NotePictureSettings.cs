namespace Orbit.Api.Notes.Pictures;

/// <summary>
/// Where a note's pictures are kept. On Azure a storage account of its own - not orbitdownloads, whose
/// blobs are anonymous-read on purpose (see info/azure-setup.md) - reached by a connection string that is
/// a Container App secret; locally a directory, so the compose stack and a plain `dotnet run` keep
/// pictures without a storage emulator. Which of the two is decided by whether the connection string is
/// set, the way the telemetry exporter is decided by its own - see Program.cs.
/// </summary>
public sealed class NotePictureSettings
{
    public const string SectionName = "NotePictures";

    /// <summary>The storage account's connection string - a secret, so only ever from the environment or a Container App secret.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>The container inside that account. Private: nothing about it is served as a plain link.</summary>
    public string Container { get; set; } = "note-pictures";

    /// <summary>Where the bytes go when there is no storage account - under the content root unless told otherwise.</summary>
    public string? Directory { get; set; }
}
