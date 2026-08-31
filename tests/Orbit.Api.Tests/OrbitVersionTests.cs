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
    [Fact]
    public void A_stamped_build_says_its_number_and_the_commit_it_came_from()
    {
        var version = Read("0.1.32+51536f360a130d98b3b631da81dce22e38c0903a");

        Assert.Equal("0.1.32", version.Version);
        Assert.Equal("51536f360a130d98b3b631da81dce22e38c0903a", version.CommitHash);
    }

    [Fact]
    public void The_short_form_is_the_one_anybody_reads()
    {
        var version = Read("0.1.32+51536f360a130d98b3b631da81dce22e38c0903a");

        Assert.Equal("ver:0.1.32+gitHash:51536f3", version.Short);
    }

    [Fact]
    public void The_long_form_is_the_one_a_checkout_takes()
    {
        var version = Read("0.1.32+51536f360a130d98b3b631da81dce22e38c0903a");

        Assert.Equal("ver:0.1.32+gitHash:51536f360a130d98b3b631da81dce22e38c0903a", version.Full);
    }

    [Fact]
    public void A_build_nobody_numbered_says_so_rather_than_inventing_a_number()
    {
        // "1.0.0" is what the SDK writes when nothing was passed. Showing it would put a version this
        // repository has never shipped into the footer somebody pastes into a bug report.
        Assert.Equal(OrbitVersion.Unknown, Read("1.0.0"));
        Assert.Equal(OrbitVersion.Unknown, Read(""));
        Assert.Equal(OrbitVersion.Unknown, Read(null));
    }

    [Fact]
    public void A_number_with_no_commit_behind_it_still_reads_as_a_version()
    {
        // A build numbered by hand - the release workflow accepts a version as input - carries no hash,
        // and a trailing "+gitHash:" with nothing after it would look like something went wrong.
        var version = Read("0.2.0");

        Assert.Equal("ver:0.2.0", version.Short);
        Assert.Equal("ver:0.2.0", version.Full);
    }

    [Fact]
    public void A_hash_shorter_than_the_short_form_is_not_cut_further()
    {
        var version = Read("0.1.1+abc");

        Assert.Equal("ver:0.1.1+gitHash:abc", version.Short);
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
