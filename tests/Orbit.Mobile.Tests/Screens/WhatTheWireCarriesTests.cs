using Orbit.Contracts.Inventory;
using Orbit.Contracts.Tasks;
using Orbit.Core.Inventory;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Inventory;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Every date the phone sends leaves it in UTC.
///
/// Not a preference: Npgsql refuses a DateTimeOffset with a non-zero offset for a "timestamp with time
/// zone" column outright, so a date sent with the picker's own offset answered 500. The save was queued,
/// retried five times and given up on - the entry kept its date on the phone and the server never heard
/// of it, which is the worst shape a failure can take because nothing on screen said so.
///
/// It only ever showed up east or west of Greenwich, which is why it survived so long - and is the one
/// limit of these tests: on a machine running in UTC the offset is zero either way, so they guard the
/// bug exactly where it can happen and say nothing where it cannot.
/// </summary>
public sealed class WhatTheWireCarriesTests
{
    [Fact]
    public void A_due_date_leaves_the_phone_in_utc()
    {
        var editor = TaskItemEditor.For(
            new TaskItemDto(
                Guid.NewGuid(), "Collect the parcel", null, false, null, "None", false, "None",
                new TimeOnly(9, 0), "Checklist", "", null),
            new Translations(new InMemoryLanguageStore()), [], []);

        editor.HasDueDate = true;
        editor.DueDate = new DateTime(2026, 8, 30);

        var sent = editor.ToDto().DueDateUtc;
        Assert.NotNull(sent);
        Assert.Equal(TimeSpan.Zero, sent.Value.Offset);
        // And still the day it was set to, wherever it is read - a conversion that moved the date would
        // trade one bug for a quieter one.
        Assert.Equal(new DateTime(2026, 8, 30), sent.Value.LocalDateTime.Date);
    }

    [Fact]
    public void An_expiry_date_leaves_the_phone_in_utc()
    {
        var editor = WarehouseItemEditor.For(
            new WarehouseItemDto(
                Guid.NewGuid(), "Milk", "Bottle", "Fridge", 1, null, nameof(InventoryUnit.Piece), null, "None"),
            new Translations(new InMemoryLanguageStore()));

        editor.ChosenExpiryUnit = ExpiryUnitChoice.For(editor.ExpiryUnits, ExpiryUnit.Days);
        editor.ExpiresIn = "3";

        var sent = editor.ToDto().ExpiryDate;
        Assert.NotNull(sent);
        Assert.Equal(TimeSpan.Zero, sent.Value.Offset);
        Assert.Equal(DateTime.Today.AddDays(3), sent.Value.LocalDateTime.Date);
    }

}
