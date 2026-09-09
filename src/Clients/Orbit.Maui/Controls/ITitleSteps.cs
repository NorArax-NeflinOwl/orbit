using System.Windows.Input;

namespace Orbit.Maui.Controls;

/// <summary>
/// A screen that is one of a series, and can be stepped through from the bar.
///
/// The design's top panel is [previous] [the screen's name and its menu] [next], with the two arrows
/// optional - most screens are not one of anything and show neither. The calendar is: a day, a month
/// and a year all have a one before and a one after, and stepping between them is the thing that
/// screen is most often asked to do.
///
/// Separate from <see cref="ITitleMenu"/> because the two are independent: a screen may have a menu
/// and no series, a series and no menu, or both. A page that implements neither gets a bar with
/// nothing in the middle but its own name, which is what almost every page wants.
/// </summary>
public interface ITitleSteps
{
    /// <summary>The one before this. Null on a screen that only steps forwards, and the arrow is left out.</summary>
    ICommand? PreviousCommand { get; }

    /// <summary>The one after this.</summary>
    ICommand? NextCommand { get; }

    /// <summary>What the arrows are, in words - a screen reader lands on an arrow with nothing else to read.</summary>
    string PreviousDescription { get; }

    /// <inheritdoc cref="PreviousDescription"/>
    string NextDescription { get; }
}
