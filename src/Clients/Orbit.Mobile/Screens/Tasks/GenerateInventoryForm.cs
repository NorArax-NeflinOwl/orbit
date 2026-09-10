using CommunityToolkit.Mvvm.ComponentModel;
using Orbit.Contracts.Inventories;
using Orbit.Contracts.Tasks;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// What to build, asked before anything is built - the phone's half of Orbit.Web's
/// GenerateInventoryOverlay.
///
/// Turning a list into a storage was one press with no questions here: the storage took the list's name
/// whether or not that was what the shelf should be called, and the "Restock supplies" list it keeps was
/// created with the defaults and had to be found and corrected afterwards - in a browser, since the
/// phone draws two of those settings and only once the shelf exists. Both answers are wanted at the same
/// moment, so both are asked at it.
///
/// Its own object rather than six more fields on the stock check: it exists between one press and the
/// next, and the panel around it is about a question that has already been answered.
/// </summary>
public sealed partial class GenerateInventoryForm : ObservableObject
{
    private readonly Translations _translations;

    public GenerateInventoryForm(Translations translations, string defaultName)
    {
        _translations = translations;
        DefaultName = defaultName;
        Channels = NotificationChannelChoice.All(translations);
        ChosenChannel = NotificationChannelChoice.For(Channels, nameof(Orbit.Core.Notifications.NotificationChannel.Push));
    }

    /// <summary>
    /// What the storage is called while nobody has typed anything - the list's own title, shown as the
    /// box's placeholder rather than filled in, because an untouched box means "the same as the list"
    /// and that is what the server reads a blank name as (see GenerateInventoryRequest).
    /// </summary>
    public string DefaultName { get; }

    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// Whether the storage keeps a restock list at all. Everything below it means nothing while it is
    /// off, which is what <see cref="KeepsAList"/> hides rather than explains.
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled = true;

    public bool KeepsAList => IsEnabled;

    partial void OnIsEnabledChanged(bool value) => OnPropertyChanged(nameof(KeepsAList));

    /// <summary>Whether that list carries the standing daily "Update stock levels" reminder.</summary>
    [ObservableProperty]
    private bool _remindDaily = true;

    /// <summary>The hour and the channel both hang off the reminder: neither means anything without it.</summary>
    public bool IsReminded => IsEnabled && RemindDaily;

    partial void OnRemindDailyChanged(bool value) => OnPropertyChanged(nameof(IsReminded));

    [ObservableProperty]
    private TimeSpan _refreshTime = RestockListSettings.DefaultRefreshTimeOfDay.ToTimeSpan();

    public IReadOnlyList<NotificationChannelChoice> Channels { get; }

    [ObservableProperty]
    private NotificationChannelChoice? _chosenChannel;

    /// <summary>
    /// Whether the list asks only about what somebody marked to look at every round, rather than about
    /// everything below its own minimum. Two answers rather than a switch nobody can read: what changes
    /// is said in words beside it - see <see cref="WhatTheListWillAskAbout"/>.
    /// </summary>
    [ObservableProperty]
    private bool _onlyCheckedRegularly;

    partial void OnOnlyCheckedRegularlyChanged(bool value) => OnPropertyChanged(nameof(WhatTheListWillAskAbout));

    /// <inheritdoc cref="OnlyCheckedRegularly"/>
    public string WhatTheListWillAskAbout
        => _translations[OnlyCheckedRegularly
            ? "The list asks only about the products marked to look at every round. What is running low but not marked is left off."
            : "The list asks for everything on this shelf that has dropped below its own minimum."];

    /// <summary>
    /// What is asked for. The restock settings travel whole rather than as the fields somebody touched:
    /// this is the storage's first and so far only answer about its list, and half of one would leave
    /// the rest at defaults nobody was shown - the same reason Orbit.Web's overlay sends all of them.
    /// </summary>
    public GenerateInventoryRequest ToRequest()
        => new(
            Name.Trim(),
            new RestockListSettingsDto(
                OnlyLinkedWithDueDate: false,
                TimeOnly.FromTimeSpan(RefreshTime),
                IsEnabled,
                RemindDaily,
                nameof(ItemPriority.Normal),
                OnlyCheckedRegularly,
                ChosenChannel?.Value ?? nameof(Orbit.Core.Notifications.NotificationChannel.Push)));
}
