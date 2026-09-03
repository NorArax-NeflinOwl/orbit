using System.Net;
using System.Text;
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
        Services.AddSingleton(SuggestingNothing());
        // Empty, which is what every page sees unless the map sent somebody to it - the same reason
        // Translations is here. A test about the handover puts a place in it first.
        Services.AddSingleton(new ChosenPlace());
        // The inventory page reads the order this reader put their inventories in. StubJSRuntime is
        // the one that answers localStorage, and it starts empty - so nothing is arranged, which is
        // the right answer for a test that has not arranged anything.
        Services.AddSingleton(new InventoryArrangement(new StubJSRuntime()));
        // The contacts page reads which conversations this reader keeps at the top. Same storage, same
        // empty start - nothing is pinned until a test pins something.
        Services.AddSingleton(new ConversationPins(new StubJSRuntime()));
        // Every overflow menu asks JS to place it inside the viewport when it opens - see
        // OverflowMenu and menuAnchor.js. There is no layout to measure here, so it answers and does
        // nothing; without it any test that opens a menu fails on the interop call rather than on
        // whatever it was about.
        JSInterop.SetupModule("./js/menuAnchor.js").SetupVoid("anchorToTrigger", _ => true).SetVoidResult();
        // The one field a task list and an inventory are named in draws itself through a module too - see
        // ChecklistTextEditor, which the note editor and TitledDescription both use. Same reason as the
        // menu above: without this, every editor test fails on an interop call rather than on whatever
        // it was about. Answered rather than made loose, so a call nobody expected is still an error.
        var checklistEditor = JSInterop.SetupModule("./js/checklistTextEditor.js");
        checklistEditor.SetupVoid("initialize", _ => true).SetVoidResult();
        checklistEditor.SetupVoid("insertChecklistItem", _ => true).SetVoidResult();
        checklistEditor.SetupVoid("dispose", _ => true).SetVoidResult();
        // Nothing was typed into a surface that does not exist, so it reports the lines it was given.
        checklistEditor.Setup<string>("getLinesAsJson", _ => true).SetResult("[]");
    }

    /// <summary>
    /// Name suggestions that never suggest anything. Every editor now carries the control that asks for
    /// them, so leaving this out fails an editor test for a reason that has nothing to do with what the
    /// test is about - the same reason Translations is here. A test that is actually about suggestions
    /// registers its own client over this one.
    /// </summary>
    private static NameSuggestionsApiClient SuggestingNothing()
        => new(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        });
}
