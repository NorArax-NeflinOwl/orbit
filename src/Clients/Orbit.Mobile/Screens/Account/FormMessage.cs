using CommunityToolkit.Mvvm.ComponentModel;

namespace Orbit.Mobile.Screens.Account;

/// <summary>
/// What the last press of one form's button said, drawn under that button - see
/// AccountViewModel.UserNameMessage and its two neighbours. The text and whether it is a refusal are
/// always said together, so they are one object rather than two loose properties per form.
/// </summary>
public sealed partial class FormMessage : ObservableObject
{
    [ObservableProperty]
    private string _text = string.Empty;

    /// <summary>Whether it says the change did not happen, which the page draws in the danger colour.</summary>
    [ObservableProperty]
    private bool _isFailure;

    public bool IsShown => Text.Length > 0;

    public void Say(string text, bool isFailure)
    {
        IsFailure = isFailure;
        Text = text;
    }

    partial void OnTextChanged(string value) => OnPropertyChanged(nameof(IsShown));
}
