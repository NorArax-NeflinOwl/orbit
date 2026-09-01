using System.Reflection;
using System.Reflection.Emit;
using Orbit.Core;
using Xunit;

namespace Orbit.Api.Tests;

/// <summary>
/// What a build calls itself. The number is counted from the history at build time
/// (ci/compute-version.sh) and stamped into the assembly, so what is tested here is the reading of it -
/// and, more to the point, what happens when there is nothing to read.
/// </summary>
public sealed class OrbitVersionTests
{
    /// <summary>
    /// That the flag actually follows the configuration, rather than being a constant somebody could set
    /// once and forget. Asserted against this project's own DEBUG symbol: the two are compiled by the
    /// same command, so they agree only if the propagation works.
    /// </summary>
    [Fact]
    public void Whether_the_commit_is_shown_follows_the_configuration_this_was_built_in()
    {
#if DEBUG
        Assert.True(OrbitVersion.IsADebugBuild);
#else
        Assert.False(OrbitVersion.IsADebugBuild);
#endif
    }

    [Fact]
    public void A_stamped_build_says_its_number_and_the_commit_it_came_from()
    {
        var version = Read("0.1.32+51536f360a130d98b3b631da81dce22e38c0903a");

        Assert.Equal("0.1.32", version.Version);
        Assert.Equal("51536f360a130d98b3b631da81dce22e38c0903a", version.CommitHash);
        // Whether it is said out loud follows the configuration this was compiled in, and nothing else.
        Assert.Equal(OrbitVersion.IsADebugBuild, version.ShowsTheCommit);
    }

    [Fact]
    public void While_debugging_the_short_form_is_the_one_anybody_reads()
    {
        var version = ADebugBuild();

        Assert.Equal("ver:0.1.32+gitHash:51536f3", version.Short);
        Assert.True(version.CanShowTheWholeCommit);
    }

    [Fact]
    public void While_debugging_the_long_form_is_the_one_a_checkout_takes()
    {
        Assert.Equal("ver:0.1.32+gitHash:51536f360a130d98b3b631da81dce22e38c0903a", ADebugBuild().Full);
    }

    [Fact]
    public void A_released_build_says_the_number_and_stops()
    {
        var version = AReleasedBuild();

        // Which commit it was cut from is a question for whoever has the repository. The number is what
        // somebody reporting a problem needs and what the update gate compares.
        Assert.Equal("ver:0.1.32", version.Short);
    }

    [Fact]
    public void A_released_build_reveals_nothing_when_pressed()
    {
        var version = AReleasedBuild();

        // Full is the same as Short, and nothing offers the press: a number that looks pressable and
        // then does nothing is worse than one that plainly is not.
        Assert.Equal(version.Short, version.Full);
        Assert.False(version.CanShowTheWholeCommit);
    }

    [Fact]
    public void A_released_build_still_knows_the_commit_it_just_does_not_say_it()
    {
        // Not hidden by being thrown away - the stamp is still there for anything that has a reason to
        // read it. What changes is only what is put in front of a reader.
        Assert.Equal("51536f360a130d98b3b631da81dce22e38c0903a", AReleasedBuild().CommitHash);
    }

    [Fact]
    public void A_build_nobody_numbered_says_so_rather_than_inventing_a_number()
    {
        // "1.0.0" is what the SDK writes when nothing was passed. Showing it would put a version this
        // repository has never shipped into the footer somebody pastes into a bug report.
        Assert.Equal("0.0.0-dev", Read("1.0.0").Version);
        Assert.Equal("0.0.0-dev", Read("").Version);
        Assert.Equal("0.0.0-dev", Read(null).Version);
    }

    /// <summary>
    /// An unnumbered build still knows which commit it is, and while debugging that is the whole point
    /// of the line: nobody is comparing "0.0.0-dev" against anything, they are asking which code is
    /// running. The SDK stamps the real HEAD next to its own "1.0.0" default, and this used to be
    /// thrown away with the number - so a Debug build showed no hash at all and the footer was not
    /// pressable, which is exactly the case the hash exists for.
    /// </summary>
    [Fact]
    public void A_build_nobody_numbered_still_says_which_commit_it_is()
    {
        var version = Read("1.0.0+86ba7a930dee2c50d3b2af03477e778354314c58");

        Assert.Equal("0.0.0-dev", version.Version);
        Assert.Equal("86ba7a930dee2c50d3b2af03477e778354314c58", version.CommitHash);
    }

    /// <summary>And it is pressable, because there is more of the hash to reveal.</summary>
    [Fact]
    public void An_unnumbered_debug_build_shows_the_hash_and_can_be_opened()
    {
        var version = Read("1.0.0+86ba7a930dee2c50d3b2af03477e778354314c58") with { ShowsTheCommit = true };

        Assert.Equal("ver:0.0.0-dev+gitHash:86ba7a9", version.Short);
        Assert.Equal("ver:0.0.0-dev+gitHash:86ba7a930dee2c50d3b2af03477e778354314c58", version.Full);
        Assert.True(version.CanShowTheWholeCommit);
    }

    /// <summary>Released, it still says nothing about the commit - the number alone, and not pressable.</summary>
    [Fact]
    public void An_unnumbered_released_build_still_says_nothing_about_the_commit()
    {
        var version = Read("1.0.0+86ba7a930dee2c50d3b2af03477e778354314c58") with { ShowsTheCommit = false };

        Assert.Equal("ver:0.0.0-dev", version.Short);
        Assert.False(version.CanShowTheWholeCommit);
    }

    [Fact]
    public void A_number_with_no_commit_behind_it_still_reads_as_a_version()
    {
        // A build numbered by hand - the release workflow accepts a version as input - carries no hash,
        // and a trailing "+gitHash:" with nothing after it would look like something went wrong.
        var version = new OrbitVersion("0.2.0", string.Empty, ShowsTheCommit: true);

        Assert.Equal("ver:0.2.0", version.Short);
        Assert.Equal("ver:0.2.0", version.Full);
    }

    /// <summary>
    /// The two builds, made rather than compiled for: which one a test run happens to be is not
    /// something the rule should depend on being able to assert.
    /// </summary>
    private static OrbitVersion ADebugBuild()
        => new("0.1.32", "51536f360a130d98b3b631da81dce22e38c0903a", ShowsTheCommit: true);

    private static OrbitVersion AReleasedBuild()
        => new("0.1.32", "51536f360a130d98b3b631da81dce22e38c0903a", ShowsTheCommit: false);

    [Fact]
    public void A_hash_shorter_than_the_short_form_is_not_cut_further()
    {
        var version = new OrbitVersion("0.1.1", "abc", ShowsTheCommit: true);

        Assert.Equal("ver:0.1.1+gitHash:abc", version.Short);
        // And there is no longer form to reveal, so nothing offers to.
        Assert.False(version.CanShowTheWholeCommit);
    }

    /// <summary>
    /// An assembly carrying one informational version, which is the only thing OrbitVersion reads. Built
    /// rather than found, because the assemblies in this test run carry whatever the SDK gave them.
    /// </summary>
    private static OrbitVersion Read(string? informationalVersion)
    {
        var name = new AssemblyName($"VersionStamp{Guid.NewGuid():N}");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);

        if (informationalVersion is not null)
        {
            assembly.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!,
                [informationalVersion]));
        }

        return OrbitVersion.ReadFrom(assembly);
    }
}
