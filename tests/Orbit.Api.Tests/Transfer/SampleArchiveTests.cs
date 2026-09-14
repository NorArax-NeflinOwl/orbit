using System.Text.Json;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Tasks;
using Orbit.Core.Transfer;
using Orbit.Core.Transfer.ImportArchive;
using Xunit;

namespace Orbit.Api.Tests.Transfer;

/// <summary>
/// Covers the archives kept under samples/: they are handed to people to import, so a change to the
/// archive format that leaves one unreadable has to fail here rather than in somebody's account. Read
/// the way the Options page reads an uploaded file, and imported through the real handler, because
/// "it is valid JSON" is not the promise the file makes.
/// </summary>
public sealed class SampleArchiveTests
{
    private const string FirstTimeFatherChecklist = "first-time-father-checklist.orbit.json";

    [Fact]
    public void A_sample_archive_is_written_in_the_version_this_build_reads()
        => Assert.Equal(OrbitArchive.CurrentVersion, Read(FirstTimeFatherChecklist).Version);

    [Fact]
    public async Task The_first_time_father_checklist_imports_whole()
    {
        var archive = Read(FirstTimeFatherChecklist);
        var account = new ImportedAccount();

        var result = await account.ImportAsync(archive);

        Assert.Equal(archive.TaskLists.Count, result.TaskLists);
        var imported = await account.TaskListsAsync();
        Assert.Equal(
            archive.TaskLists.Select(taskList => taskList.Title).Order(),
            imported.Select(taskList => taskList.Title).Order());
    }

    [Fact]
    public async Task Every_entry_of_the_checklist_is_filed_under_a_category()
    {
        var account = new ImportedAccount();
        await account.ImportAsync(Read(FirstTimeFatherChecklist));

        var unfiled = (await account.TaskListsAsync())
            .SelectMany(taskList => taskList.Items)
            .Where(item => item.Categories.Count == 0);

        Assert.Empty(unfiled);
    }

    [Fact]
    public async Task The_group_list_stands_for_every_other_list_in_the_file()
    {
        var account = new ImportedAccount();
        await account.ImportAsync(Read(FirstTimeFatherChecklist));
        var imported = await account.TaskListsAsync();

        var group = Assert.Single(imported.Where(taskList => taskList.IsGroup));
        // Ids rather than titles: a link the file names but the file does not carry is dropped on
        // import, so counting resolved ids is what says the group still gathers everything.
        Assert.Equal(
            imported.Where(taskList => !taskList.IsGroup).Select(taskList => taskList.Id).Order(),
            group.Items.SelectMany(item => item.LinkedTaskListIds).Order());
    }

    private static OrbitArchive Read(string fileName)
    {
        var path = Path.Combine(RepositoryRoot(), "samples", fileName);
        return JsonSerializer.Deserialize<OrbitArchive>(
            File.ReadAllText(path),
            // The options the Options page reads an uploaded file with - anything it accepts, this must.
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"{fileName} did not contain an archive.");
    }

    /// <summary>The tests run from bin/, so the repository is found by walking up to the solution.</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Orbit.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find Orbit.sln above the test binaries.");
    }

    /// <summary>An empty account with nothing but the repositories an import writes to.</summary>
    private sealed class ImportedAccount
    {
        private readonly InMemoryNoteRepository _noteRepository = new();
        private readonly InMemoryTaskRepository _taskRepository = new();
        private readonly InMemoryCalendarEventRepository _calendarEventRepository = new();
        private readonly InMemoryInventoryRepository _inventoryRepository = new();
        private readonly InMemoryInventoryItemRepository _inventoryItemRepository = new();
        private readonly InMemoryPlaceRepository _placeRepository = new();
        private readonly InMemoryTagColourRepository _tagColourRepository = new();

        private Guid UserId { get; } = Guid.NewGuid();

        public Task<ImportArchiveResult> ImportAsync(OrbitArchive archive)
            => new ImportArchiveCommandHandler(
                    _noteRepository, _taskRepository, _calendarEventRepository, _inventoryRepository,
                    _inventoryItemRepository, _placeRepository, _tagColourRepository)
                .HandleAsync(new ImportArchiveCommand(UserId, archive), CancellationToken.None);

        public Task<IReadOnlyList<TaskList>> TaskListsAsync()
            => _taskRepository.GetAllAsync(UserId, updatedSinceUtc: null, CancellationToken.None);
    }
}
