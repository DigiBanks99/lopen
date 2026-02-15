using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Storage.Tests;

public sealed class PlanManagerTests
{
    private const string ProjectRoot = "/test/project";
    private readonly InMemoryFileSystem _fs = new();
    private readonly PlanManager _sut;

    public PlanManagerTests()
    {
        _sut = new PlanManager(_fs, NullLogger<PlanManager>.Instance, ProjectRoot);
    }

    private static string SamplePlan() => """
        # Auth Module Plan

        - [ ] Set up authentication
          - [ ] Implement token storage
          - [ ] Add refresh logic
        - [x] Configure endpoints
        - [ ] Write tests
        """;

    // --- Constructor validation ---

    [Fact]
    public void Constructor_NullFileSystem_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PlanManager(null!, NullLogger<PlanManager>.Instance, ProjectRoot));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PlanManager(new InMemoryFileSystem(), null!, ProjectRoot));
    }

    [Fact]
    public void Constructor_EmptyProjectRoot_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new PlanManager(new InMemoryFileSystem(), NullLogger<PlanManager>.Instance, ""));
    }

    // --- WritePlanAsync ---

    [Fact]
    public async Task WritePlanAsync_CreatesPlanFile()
    {
        await _sut.WritePlanAsync("auth", "# Plan\n- [ ] Task 1");

        Assert.True(await _sut.PlanExistsAsync("auth"));
    }

    [Fact]
    public async Task WritePlanAsync_ContentIsReadable()
    {
        var content = "# Plan\n- [ ] Task 1\n- [x] Task 2";
        await _sut.WritePlanAsync("auth", content);

        var read = await _sut.ReadPlanAsync("auth");
        Assert.Equal(content, read);
    }

    [Fact]
    public async Task WritePlanAsync_OverwritesExisting()
    {
        await _sut.WritePlanAsync("auth", "old content");
        await _sut.WritePlanAsync("auth", "new content");

        var read = await _sut.ReadPlanAsync("auth");
        Assert.Equal("new content", read);
    }

    [Fact]
    public async Task WritePlanAsync_EmptyModule_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.WritePlanAsync("", "content"));
    }

    [Fact]
    public async Task WritePlanAsync_NullContent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.WritePlanAsync("auth", null!));
    }

    // --- ReadPlanAsync ---

    [Fact]
    public async Task ReadPlanAsync_NoPlan_ReturnsNull()
    {
        var result = await _sut.ReadPlanAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task ReadPlanAsync_EmptyModule_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ReadPlanAsync(""));
    }

    // --- PlanExistsAsync ---

    [Fact]
    public async Task PlanExistsAsync_NoPlan_ReturnsFalse()
    {
        Assert.False(await _sut.PlanExistsAsync("nonexistent"));
    }

    [Fact]
    public async Task PlanExistsAsync_AfterWrite_ReturnsTrue()
    {
        await _sut.WritePlanAsync("auth", "content");
        Assert.True(await _sut.PlanExistsAsync("auth"));
    }

    // --- UpdateCheckboxAsync ---

    [Fact]
    public async Task UpdateCheckboxAsync_ChecksUncheckedTask()
    {
        await _sut.WritePlanAsync("auth", "- [ ] Set up authentication\n- [ ] Write tests");

        var updated = await _sut.UpdateCheckboxAsync("auth", "Set up authentication", true);

        Assert.True(updated);
        var content = await _sut.ReadPlanAsync("auth");
        Assert.Contains("- [x] Set up authentication", content);
    }

    [Fact]
    public async Task UpdateCheckboxAsync_UnchecksCheckedTask()
    {
        await _sut.WritePlanAsync("auth", "- [x] Configure endpoints\n- [ ] Write tests");

        var updated = await _sut.UpdateCheckboxAsync("auth", "Configure endpoints", false);

        Assert.True(updated);
        var content = await _sut.ReadPlanAsync("auth");
        Assert.Contains("- [ ] Configure endpoints", content);
    }

    [Fact]
    public async Task UpdateCheckboxAsync_PreservesIndentation()
    {
        await _sut.WritePlanAsync("auth", "- [ ] Parent\n  - [ ] Child task\n  - [ ] Other child");

        var updated = await _sut.UpdateCheckboxAsync("auth", "Child task", true);

        Assert.True(updated);
        var content = await _sut.ReadPlanAsync("auth");
        Assert.Contains("  - [x] Child task", content);
    }

    [Fact]
    public async Task UpdateCheckboxAsync_TaskNotFound_ReturnsFalse()
    {
        await _sut.WritePlanAsync("auth", "- [ ] Existing task");

        var updated = await _sut.UpdateCheckboxAsync("auth", "Nonexistent task", true);

        Assert.False(updated);
    }

    [Fact]
    public async Task UpdateCheckboxAsync_NoPlan_ReturnsFalse()
    {
        var updated = await _sut.UpdateCheckboxAsync("auth", "Some task", true);
        Assert.False(updated);
    }

    [Fact]
    public async Task UpdateCheckboxAsync_EmptyModule_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.UpdateCheckboxAsync("", "task", true));
    }

    [Fact]
    public async Task UpdateCheckboxAsync_EmptyTaskText_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.UpdateCheckboxAsync("auth", "", true));
    }

    [Fact]
    public async Task UpdateCheckboxAsync_DoesNotAffectOtherLines()
    {
        var original = "# Plan\n- [ ] Task A\n- [ ] Task B\n- [x] Task C";
        await _sut.WritePlanAsync("auth", original);

        await _sut.UpdateCheckboxAsync("auth", "Task B", true);

        var content = await _sut.ReadPlanAsync("auth");
        Assert.Contains("- [ ] Task A", content);
        Assert.Contains("- [x] Task B", content);
        Assert.Contains("- [x] Task C", content);
        Assert.Contains("# Plan", content);
    }

    // --- ReadTasksAsync ---

    [Fact]
    public async Task ReadTasksAsync_ParsesCheckboxes()
    {
        await _sut.WritePlanAsync("auth", "- [ ] Task A\n- [x] Task B\n- [ ] Task C");

        var tasks = await _sut.ReadTasksAsync("auth");

        Assert.Equal(3, tasks.Count);
        Assert.Equal("Task A", tasks[0].Text);
        Assert.False(tasks[0].IsCompleted);
        Assert.Equal("Task B", tasks[1].Text);
        Assert.True(tasks[1].IsCompleted);
    }

    [Fact]
    public async Task ReadTasksAsync_CalculatesLevels()
    {
        await _sut.WritePlanAsync("auth", "- [ ] Top\n  - [ ] Child\n    - [ ] Grandchild");

        var tasks = await _sut.ReadTasksAsync("auth");

        Assert.Equal(3, tasks.Count);
        Assert.Equal(0, tasks[0].Level);
        Assert.Equal(1, tasks[1].Level);
        Assert.Equal(2, tasks[2].Level);
    }

    [Fact]
    public async Task ReadTasksAsync_IgnoresNonCheckboxLines()
    {
        await _sut.WritePlanAsync("auth", "# Plan\n\nSome text\n- [ ] Only task\n\n---");

        var tasks = await _sut.ReadTasksAsync("auth");

        Assert.Single(tasks);
        Assert.Equal("Only task", tasks[0].Text);
    }

    [Fact]
    public async Task ReadTasksAsync_NoPlan_ReturnsEmpty()
    {
        var tasks = await _sut.ReadTasksAsync("nonexistent");
        Assert.Empty(tasks);
    }

    [Fact]
    public async Task ReadTasksAsync_EmptyModule_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ReadTasksAsync(""));
    }

    [Fact]
    public async Task ReadTasksAsync_UppercaseX_RecognizedAsCompleted()
    {
        await _sut.WritePlanAsync("auth", "- [X] Task with uppercase X");

        var tasks = await _sut.ReadTasksAsync("auth");

        Assert.Single(tasks);
        Assert.True(tasks[0].IsCompleted);
    }

    // --- Multi-module isolation ---

    [Fact]
    public async Task WritePlanAsync_ModulesAreIsolated()
    {
        await _sut.WritePlanAsync("auth", "auth plan");
        await _sut.WritePlanAsync("storage", "storage plan");

        Assert.Equal("auth plan", await _sut.ReadPlanAsync("auth"));
        Assert.Equal("storage plan", await _sut.ReadPlanAsync("storage"));
    }

    // --- Full round-trip ---

    [Fact]
    public async Task RoundTrip_WriteReadUpdateRead()
    {
        var plan = "- [ ] Implement feature\n  - [ ] Write code\n  - [ ] Add tests\n- [ ] Deploy";
        await _sut.WritePlanAsync("auth", plan);

        // Check initial state
        var tasks = await _sut.ReadTasksAsync("auth");
        Assert.Equal(4, tasks.Count);
        Assert.True(tasks.All(t => !t.IsCompleted));

        // Complete a subtask
        await _sut.UpdateCheckboxAsync("auth", "Write code", true);

        tasks = await _sut.ReadTasksAsync("auth");
        Assert.True(tasks[1].IsCompleted);
        Assert.False(tasks[0].IsCompleted);
        Assert.False(tasks[2].IsCompleted);
    }
}
