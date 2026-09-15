using Orbit.Contracts.Notes;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// A picture on the phone's note screen: fetched after the lines are shown, kept on the handset for the
/// next time, and a line saying why when there is nothing to draw. What the cache itself does is tested
/// in NotePictureCacheTests; here it is that the screen asks it and shows the answer.
/// </summary>
public sealed partial class NoteDetailScreenTests
{
    private static readonly Guid PictureId = Guid.Parse("44444444-0000-4000-8000-000000000004");
    private static readonly byte[] PictureBytes = [0xFF, 0xD8, 0xFF, 0xE0, 9, 8, 7];

    private static NoteContentLineDto APictureLine()
        => new(string.Empty, IsChecklistItem: false, IsChecked: false, Picture: new NotePictureLineDto(PictureId, "image/jpeg", 640, 480));

    [Fact]
    public async Task A_picture_the_server_holds_is_drawn()
    {
        using var context = new ScreenContext();
        context.Server.Pictures[PictureId] = PictureBytes;
        var note = await context.Notes.CreateAsync("Holiday", [new("the beach", false, false), APictureLine()]);
        await context.SynchroniseAsync();

        var screen = await context.OpenAsync(note.LocalId);

        var line = screen.Lines[1];
        Assert.True(line.IsAPicture);
        Assert.True(line.ShowsPicture);
        Assert.False(line.ShowsPictureNote);
        Assert.Equal(PictureBytes, line.PictureBytes);
    }

    /// <summary>Once fetched, the picture is on the handset, and a note opened with no connection still shows it.</summary>
    [Fact]
    public async Task A_picture_fetched_once_is_drawn_offline()
    {
        using var context = new ScreenContext();
        context.Server.Pictures[PictureId] = PictureBytes;
        var note = await context.Notes.CreateAsync("Holiday", [APictureLine()]);
        await context.SynchroniseAsync();
        await context.OpenAsync(note.LocalId);

        context.Server.IsUnreachable = true;
        var screen = await context.OpenAsync(note.LocalId);

        Assert.Equal(PictureBytes, screen.Lines[0].PictureBytes);
    }

    [Fact]
    public async Task A_picture_that_cannot_be_fetched_says_so_in_its_place()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Holiday", [APictureLine()]);
        await context.SynchroniseAsync();
        context.Server.IsUnreachable = true;

        var screen = await context.OpenAsync(note.LocalId);

        var line = screen.Lines[0];
        Assert.False(line.ShowsPicture);
        Assert.True(line.ShowsPictureNote);
        Assert.Contains("online", line.PictureNote);
    }

    /// <summary>A picture line is still a picture line after a round through the screen: nothing about the bytes is written back.</summary>
    [Fact]
    public async Task A_picture_line_survives_an_edit_and_a_save()
    {
        using var context = new ScreenContext();
        context.Server.Pictures[PictureId] = PictureBytes;
        var note = await context.Notes.CreateAsync("Holiday", [APictureLine(), new("the beach", false, false)]);
        await context.SynchroniseAsync();
        var screen = await context.OpenAsync(note.LocalId);

        screen.Lines[1].Text = "the beach at dusk";
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var stored = (await context.Notes.FindAsync(note.LocalId))!;
        Assert.Equal(PictureId, stored.Content[0].Picture!.PictureId);
        Assert.Equal("the beach at dusk", stored.Content[1].Text);
    }
}
