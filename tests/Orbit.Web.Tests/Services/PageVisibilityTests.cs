using Bunit;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Whether this tab is in front of somebody, which is what the chat poll asks before every tick.
/// The answer when it cannot be obtained matters most: a poll that stops on a failed question is a
/// chat that silently goes quiet, and quiet is indistinguishable from nobody writing.
/// </summary>
public sealed class PageVisibilityTests : IDisposable
{
    private readonly TestContext _context = new();

    [Fact]
    public async Task A_tab_in_front_of_somebody_says_so()
    {
        _context.JSInterop.SetupModule("./js/presence.js").Setup<bool>("isPageVisible").SetResult(true);

        Assert.True(await new PageVisibility(_context.JSInterop.JSRuntime).IsPageVisibleAsync());
    }

    [Fact]
    public async Task A_tab_behind_others_says_so_too()
    {
        _context.JSInterop.SetupModule("./js/presence.js").Setup<bool>("isPageVisible").SetResult(false);

        Assert.False(await new PageVisibility(_context.JSInterop.JSRuntime).IsPageVisibleAsync());
    }

    [Fact]
    public async Task A_question_that_cannot_be_asked_counts_as_visible()
    {
        // Erring the other way would stop the poll over a script that broke, and a chat that has gone
        // quiet for that reason looks exactly like one where nobody is writing.
        _context.JSInterop.SetupModule("./js/presence.js").Setup<bool>("isPageVisible")
            .SetException(new Microsoft.JSInterop.JSException("presence.js is not itself today."));

        Assert.True(await new PageVisibility(_context.JSInterop.JSRuntime).IsPageVisibleAsync());
    }

    public void Dispose() => _context.Dispose();
}
