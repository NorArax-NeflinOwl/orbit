using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// How a screen is finished - saved, cancelled, deleted, or left by its own Back - so that the browser's
/// Back button afterwards does not reopen it.
///
/// Every editor used to end with an ordinary navigation to wherever it should end, which pushed that page
/// on top of the form: Back from there reopened the form with what had been typed before saving, and for
/// a new note that is one press from saving it twice. <see cref="FinishAsync"/> steps back instead when
/// the destination is the entry right before this one - which is what finishing a form opened from a list
/// or a summary nearly always is - and replaces the page otherwise. See <see cref="InAppHistory"/> for
/// the rules and for why reading the history cannot leave the app.
///
/// Where to go is still <see cref="ReturnTo"/>'s question; this only decides how. Scoped, which in
/// WebAssembly is one for the life of the app, and made by App.razor so it has watched from the first
/// page rather than from the first page that happened to ask for it.
/// </summary>
public sealed class NavigationTrail : IDisposable
{
    private readonly NavigationManager _navigationManager;
    private readonly IJSRuntime _jsRuntime;
    private readonly InAppHistory _history;

    public NavigationTrail(NavigationManager navigationManager, IJSRuntime jsRuntime)
    {
        _navigationManager = navigationManager;
        _jsRuntime = jsRuntime;
        _history = new InAppHistory(Normalise(navigationManager.Uri));
        _navigationManager.LocationChanged += OnLocationChanged;
    }

    /// <summary>
    /// The page on screen, as a path on this site with its query - what a page hands on as the returnTo of
    /// a screen it opens, so that screen finishes back here. Includes this page's own returnTo, which is
    /// what lets a summary still find its way back to wherever it was opened from afterwards.
    /// </summary>
    public string Here => "/" + _navigationManager.ToBaseRelativePath(_navigationManager.Uri);

    /// <summary>Leaves this page for <paramref name="destination"/>, a path on this site, without leaving it in the history.</summary>
    public Task FinishAsync(string destination)
        => TakeAsync(_history.Leave(Normalise(destination)));

    /// <summary>
    /// Leaves a page whose subject has just been deleted, along with every page of that thing still on top
    /// of the history - its summary under its form, say - since each of them would now open on "no longer
    /// exists". <paramref name="destination"/> is followed unless it is itself one of those pages, and
    /// <paramref name="section"/> - the list the thing lived on - answers instead.
    /// </summary>
    /// <param name="deletedPath">The deleted thing's own path, "/notes/{id}" - everything beneath it goes too.</param>
    public Task FinishAfterDeletingAsync(string deletedPath, string destination, string section)
    {
        var goingTo = InAppHistory.IsUnder(destination, deletedPath) ? section : destination;
        return TakeAsync(_history.Leave(Normalise(goingTo), deletedPath));
    }

    public void Dispose() => _navigationManager.LocationChanged -= OnLocationChanged;

    private async Task TakeAsync(FinishStep step)
    {
        if (step.StepsBack > 0)
        {
            await _jsRuntime.InvokeVoidAsync("history.go", -step.StepsBack);
            return;
        }

        _navigationManager.NavigateTo(step.ReplaceWith!, replace: true);
    }

    /// <summary>
    /// Every arrival, whoever caused it - a link, a page's own navigation, or the browser's Back and
    /// Forward. The second half of a finish that stepped back and then has to replace what it arrived on
    /// is made from here, since it can only be made once the browser is there.
    /// </summary>
    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (_history.Arrived(Normalise(args.Location)) is { } replaceWith)
        {
            _navigationManager.NavigateTo(replaceWith, replace: true);
        }
    }

    /// <summary>
    /// One spelling for one address, so the address the browser reports and the one a page asked for
    /// compare equal: both are parsed the same way, and what is on this site is kept as a path.
    /// </summary>
    private string Normalise(string location)
    {
        var absolute = _navigationManager.ToAbsoluteUri(location).AbsoluteUri;
        return absolute.StartsWith(_navigationManager.BaseUri, StringComparison.Ordinal)
            ? "/" + absolute[_navigationManager.BaseUri.Length..]
            : absolute;
    }
}
