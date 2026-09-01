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
/// <param name="ShowsTheCommit">
/// Whether the commit is part of what this build says about itself. True for a Debug build and false for
/// a Release one - see <see cref="IsADebugBuild"/>.
///
/// A value rather than a compile-time check read wherever it is needed, so both answers can be built and
/// asserted on rather than only whichever one the test run happened to compile.
/// </param>
public sealed record OrbitVersion(string Version, string CommitHash, bool ShowsTheCommit)
{
    /// <summary>
    /// Whether this was built to be debugged. The one place the configuration is read: everything else
    /// takes it as a value.
    /// </summary>
    public const bool IsADebugBuild =
#if DEBUG
        true;
#else
        false;
#endif

    /// <summary>
    /// What a local `dotnet run` says, before any commit is read off it. Deliberately not "0.1.0": a
    /// made-up number that looks real is worse than one that says it is not, and this is the string
    /// somebody pastes into a bug report. <see cref="ReadFrom"/> keeps whatever commit was stamped
    /// alongside it.
    /// </summary>
    public static readonly OrbitVersion Unknown = new("0.0.0-dev", string.Empty, IsADebugBuild);

    /// <summary>Enough of the hash to find the commit by, which is all anybody reads at a glance.</summary>
    public string ShortCommitHash => CommitHash.Length > 7 ? CommitHash[..7] : CommitHash;

    /// <summary>
    /// Whether there is a longer form to reveal at all. False for a released build, where the commit is
    /// not shown - so the number is text rather than something that looks pressable and then does
    /// nothing.
    /// </summary>
    public bool CanShowTheWholeCommit => ShowsTheCommit && CommitHash.Length > ShortCommitHash.Length;

    /// <summary>
    /// What is shown: "ver:0.1.17+gitHash:51536f3" while debugging, and "ver:0.1.17" once released.
    ///
    /// A released build says the number and stops. The number is what somebody reporting a problem needs
    /// and what the update gate compares; which commit it was cut from is a question for whoever has the
    /// repository, and putting it in front of everybody else is detail about the inside of the
    /// application that a released build has no reason to volunteer.
    /// </summary>
    public string Short => ShowsTheCommit ? Describe(ShortCommitHash) : $"ver:{Version}";

    /// <summary>What a press reveals: the same, with the whole hash - which is what a `git checkout` takes.</summary>
    public string Full => ShowsTheCommit ? Describe(CommitHash) : Short;

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
        // repository ever ships - see Directory.Build.props. The number is discarded; the commit is
        // not. The SDK stamps the real HEAD beside its own default, and that hash is the whole point
        // of the line while debugging: nobody compares "0.0.0-dev" against anything, they are asking
        // which code is running. Dropping it with the number left a Debug build showing no hash and a
        // footer that could not be opened - the one case the hash exists for.
        return version is "1.0.0" or ""
            ? Unknown with { CommitHash = commitHash }
            : new OrbitVersion(version, commitHash, IsADebugBuild);
    }
}
