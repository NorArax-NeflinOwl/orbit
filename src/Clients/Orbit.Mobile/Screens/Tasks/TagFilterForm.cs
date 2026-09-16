using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Core.Tags;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>One tag on the filter form's checklist.</summary>
public sealed partial class TagFilterChoice(string name) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    private bool _isChosen;
}

/// <summary>
/// Making a filter for the dashboard's Tasks card on the phone - the tasks screen's half of what
/// Orbit.Web's TagFilterDialog does, over the same rules (see TaskTagFilters). The tags already on this
/// account's lists are a checklist, a new word can be added to it ticked, and "And" at the top turns
/// "any of these" into "all of these".
/// </summary>
public sealed partial class TagFilterForm(TaskTagFilters filters, Translations translations) : ObservableObject
{
    public ObservableCollection<TagFilterChoice> Tags { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaysSomethingWhenClosed))]
    private bool _isOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LogicHint))]
    private bool _matchesAll;

    [ObservableProperty]
    private string _newTag = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isSaving;

    /// <summary>What the form has to say - why a save did not happen, or, once closed, where the filter went.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    [NotifyPropertyChangedFor(nameof(SaysSomethingWhenClosed))]
    private string _message = string.Empty;

    public bool HasMessage => Message.Length > 0;

    /// <summary>A message left for the screen under the panel once it has closed - where the filter went.</summary>
    public bool SaysSomethingWhenClosed => HasMessage && !IsOpen;

    public bool HasNoTags => Tags.Count == 0;

    /// <summary>What the filter will find, said beside the "And" switch.</summary>
    public string LogicHint => MatchesAll
        ? translations["Lists carrying every chosen tag."]
        : translations["Lists carrying any chosen tag."];

    /// <summary>Opens the form on these tags - every one on the account's lists - with nothing chosen.</summary>
    public void Open(IEnumerable<string> knownTags)
    {
        Tags.Clear();
        foreach (var tag in TagNames.Tidy([.. knownTags.Order(StringComparer.CurrentCultureIgnoreCase)]))
        {
            Tags.Add(Watched(new TagFilterChoice(tag)));
        }

        MatchesAll = false;
        NewTag = string.Empty;
        Message = string.Empty;
        IsOpen = true;
        OnPropertyChanged(nameof(HasNoTags));
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ToggleMatchesAll() => MatchesAll = !MatchesAll;

    /// <summary>A word no list carries yet, added ticked - writing it is choosing it. One already there is ticked instead.</summary>
    [RelayCommand]
    private void AddTag()
    {
        var tag = NewTag.Trim();
        if (tag.Length == 0)
        {
            return;
        }

        var existing = Tags.FirstOrDefault(choice => TagNames.KeyOf(choice.Name) == TagNames.KeyOf(tag));
        if (existing is null)
        {
            existing = Watched(new TagFilterChoice(tag));
            Tags.Add(existing);
            OnPropertyChanged(nameof(HasNoTags));
        }

        existing.IsChosen = true;
        NewTag = string.Empty;
    }

    [RelayCommand]
    private void Cancel()
    {
        IsOpen = false;
        Message = string.Empty;
    }

    private bool CanSave => !IsSaving && Tags.Any(choice => choice.IsChosen);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        IsSaving = true;
        try
        {
            var chosen = Tags.Where(choice => choice.IsChosen).Select(choice => choice.Name).ToList();
            if (await filters.CreateAsync(chosen, MatchesAll, cancellationToken) is not { } created)
            {
                Message = translations["That filter could not be saved. Making one needs a connection."];
                return;
            }

            IsOpen = false;
            Message = translations.Format(
                "Filter \"{0}\" saved - choose it from the Tasks card's menu on the dashboard.",
                TaskTagFilters.NameOf(created, translations));
        }
        finally
        {
            IsSaving = false;
        }
    }

    private TagFilterChoice Watched(TagFilterChoice choice)
    {
        choice.PropertyChanged += (_, _) => SaveCommand.NotifyCanExecuteChanged();
        return choice;
    }
}
