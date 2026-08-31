using System.Reflection;

namespace Orbit.Core;

/// <summary>
/// Which build this is and which commit it came from, as the footer and the phone's About row say it.
///
/// Read from the assembly rather than written down in code, because the number is not something anybody
/// decides: it is counted from the history at build time (see ci/compute-version.sh) and stamped in as
/// the informational version. A constant would be a second answer to the same question, and the wrong
/// one the moment a build is made without editing it.
///
/// Each client reads **its own** assembly. The version is per project - a day that changed the phone and
/// not the web client raises one and not the other - so there is no single number a shared type could
/// hold on everybody's behalf.
/// </summary>
/// <param name="Version">"0.1.17", or <see cref="Unknown"/>'s value for a build nobody stamped.</param>
/// <param name="CommitHash">The full hash, or empty when there is none to show.</param>
public sealed record OrbitVersion(string Version, string CommitHash)
{
    /// <summary>
    /// What a local `dotnet run` says. Deliberately not "0.1.0": a made-up number that looks real is
    /// worse than one that says it is not, and this is the string somebody pastes into a bug report.
    /// </summary>
    public static readonly OrbitVersion Unknown = new("0.0.0-dev", string.Empty);

    /// <summary>Enough of the hash to find the commit by, which is all anybody reads at a glance.</summary>
    public string ShortCommitHash => CommitHash.Length > 7 ? CommitHash[..7] : CommitHash;

    /// <summary>What is shown: "ver:0.1.17+gitHash:51536f3".</summary>
    public string Short => Describe(ShortCommitHash);

    /// <summary>What a press reveals: the same, with the whole hash - which is what a `git checkout` takes.</summary>
    public string Full => Describe(CommitHash);

    private string Describe(string hash) => hash.Length == 0 ? $"ver:{Version}" : $"ver:{Version}+gitHash:{hash}";

    /// <summary>
    /// Reads the stamp off an assembly. The informational version carries "0.1.17+&lt;hash&gt;" when the
    /// build was numbered, and whatever the SDK defaulted to when it was not - which is why anything
    /// that does not look like a stamped version reads as <see cref="Unknown"/> rather than being shown
    /// as though it meant something.
    /// </summary>
    public static OrbitVersion ReadFrom(Assembly assembly)
    {
        var stamped = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(stamped))
        {
            return Unknown;
        }

        var separator = stamped.IndexOf('+');
        var version = separator < 0 ? stamped : stamped[..separator];
        var commitHash = separator < 0 ? string.Empty : stamped[(separator + 1)..];

        // "1.0.0" is what the SDK writes when nobody said otherwise, and it is not a version this
        // repository ever ships - see Directory.Build.props.
        return version is "1.0.0" or "" ? Unknown : new OrbitVersion(version, commitHash);
    }
}
