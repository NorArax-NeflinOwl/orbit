namespace Orbit.Web.Components;

/// <summary>
/// The one place a page's header and its phone toolbar agree on, so the Menu button can be written by
/// <see cref="PhoneToolbar"/> and drawn by <see cref="PageHeader"/> - two components with no way to
/// reach each other, on opposite sides of a page's own markup.
///
/// A named section rather than a parameter passed down, because the two are siblings: a page writes its
/// header and then its toolbar, and threading a render fragment from the second into the first would
/// mean every page carrying the wiring for something neither of them is about.
///
/// The button used to sit where the toolbar does, on a row of its own between the title and the first
/// card. On a phone that is a whole row spent on a button - see the CSS, where the toolbar is left with
/// nothing but its panel.
/// </summary>
public static class PageToolbarTrigger
{
    public const string SectionName = "page-toolbar-trigger";
}
