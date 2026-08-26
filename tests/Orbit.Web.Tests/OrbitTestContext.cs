using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;

namespace Orbit.Web.Tests;

/// <summary>
/// A bUnit TestContext with the services every page needs regardless of what it is testing, so each
/// test class registers only what its own subject actually uses.
///
/// Translations is the case in point: every page reads its own text through it, so leaving it out fails
/// a test for a reason that has nothing to do with what the test is about. It runs in English here,
/// which is what the assertions are written against.
/// </summary>
public abstract class OrbitTestContext : TestContext
{
    protected OrbitTestContext()
    {
        Services.AddSingleton(new Translations(new StubJSRuntime()));
    }
}
