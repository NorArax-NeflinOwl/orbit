using Orbit.Core;
using Xunit;

namespace Orbit.Web.Tests;

/// <summary>
/// The copyright line the footer, the About dialog, the Privacy and Licence pages and the phone's About
/// screen all read from one place - so pinning it here pins every one of them.
/// </summary>
public sealed class OrbitReleaseTests
{
    [Fact]
    public void The_copyright_line_names_the_person_who_publishes_orbit()
    {
        Assert.Equal("© 2026 Patryk Pudwel", OrbitRelease.Copyright);
    }
}
