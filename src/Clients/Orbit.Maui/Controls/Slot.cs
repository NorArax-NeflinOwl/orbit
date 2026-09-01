namespace Orbit.Maui.Controls;

/// <summary>
/// A place in a control that whoever uses it puts a view into - the pin on a card, the menu on a
/// person's row. Shared by ItemCard and PersonRow so that both are empty in the same way.
/// </summary>
internal static class Slot
{
    /// <summary>
    /// Puts something in, or leaves the place out altogether. Left out rather than drawn empty, and
    /// that has to follow what is in it rather than only whether anything is: a row is handed a pin
    /// that hides itself on somebody else's note, and a slot holding a hidden thing still took its
    /// place - which pushed every name in the list to the right of a pin nobody could see.
    /// </summary>
    public static void Fill(ContentView slot, object? content)
    {
        slot.Content = content as View;

        if (content is View view)
        {
            slot.SetBinding(VisualElement.IsVisibleProperty, static (View held) => held.IsVisible, source: view);
            return;
        }

        slot.RemoveBinding(VisualElement.IsVisibleProperty);
        slot.IsVisible = false;
    }
}
