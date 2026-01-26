using Shouldly;

namespace Lopen.Core.Tests;

public class LoopStateManagerTests : IDisposable
{
    private readonly string _testDir;
    private readonly LoopStateManager _stateManager;

    public LoopStateManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _stateManager = new LoopStateManager(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public void DoneFilePath_ReturnsExpectedPath()
    {
        _stateManager.DoneFilePath.ShouldBe(Path.Combine(_testDir, "lopen.loop.done"));
    }

    [Fact]
    public void JobsFilePath_ReturnsExpectedPath()
    {
        _stateManager.JobsFilePath.ShouldBe(Path.Combine(_testDir, "docs", "requirements", "jobs-to-be-done.json"));
    }

    [Fact]
    public void PlanFilePath_ReturnsExpectedPath()
    {
        _stateManager.PlanFilePath.ShouldBe(Path.Combine(_testDir, "docs", "requirements", "IMPLEMENTATION_PLAN.md"));
    }

    [Fact]
    public void IsLoopComplete_NoFile_ReturnsFalse()
    {
        _stateManager.IsLoopComplete().ShouldBeFalse();
    }

    [Fact]
    public void IsLoopComplete_FileExists_ReturnsTrue()
    {
        File.WriteAllText(_stateManager.DoneFilePath, "done");

        _stateManager.IsLoopComplete().ShouldBeTrue();
    }

    [Fact]
    public void RemoveDoneFile_FileExists_DeletesFile()
    {
        File.WriteAllText(_stateManager.DoneFilePath, "done");

        _stateManager.RemoveDoneFile();

        File.Exists(_stateManager.DoneFilePath).ShouldBeFalse();
    }

    [Fact]
    public void RemoveDoneFile_NoFile_DoesNotThrow()
    {
        Should.NotThrow(() => _stateManager.RemoveDoneFile());
    }

    [Fact]
    public async Task CreateDoneFileAsync_CreatesFile()
    {
        await _stateManager.CreateDoneFileAsync("All done!");

        File.Exists(_stateManager.DoneFilePath).ShouldBeTrue();
        var content = await File.ReadAllTextAsync(_stateManager.DoneFilePath);
        content.ShouldBe("All done!");
    }

    [Fact]
    public async Task CreateDoneFileAsync_NoReason_UsesDefaultMessage()
    {
        await _stateManager.CreateDoneFileAsync();

        var content = await File.ReadAllTextAsync(_stateManager.DoneFilePath);
        content.ShouldContain("Loop completed at");
    }

    [Fact]
    public async Task LoadPromptAsync_FileExists_ReturnsContent()
    {
        var promptPath = Path.Combine(_testDir, "PLAN.PROMPT.md");
        await File.WriteAllTextAsync(promptPath, "Plan prompt content");

        var content = await _stateManager.LoadPromptAsync("PLAN.PROMPT.md");

        content.ShouldBe("Plan prompt content");
    }

    [Fact]
    public async Task LoadPromptAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        await Should.ThrowAsync<FileNotFoundException>(async () =>
        {
            await _stateManager.LoadPromptAsync("nonexistent.md");
        });
    }

    [Fact]
    public void IsOnMainBranch_NoGitDir_ReturnsFalse()
    {
        _stateManager.IsOnMainBranch().ShouldBeFalse();
    }

    [Fact]
    public void IsOnMainBranch_OnMain_ReturnsTrue()
    {
        var gitDir = Path.Combine(_testDir, ".git");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/main");

        _stateManager.IsOnMainBranch().ShouldBeTrue();
    }

    [Fact]
    public void IsOnMainBranch_OnMaster_ReturnsTrue()
    {
        var gitDir = Path.Combine(_testDir, ".git");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/master");

        _stateManager.IsOnMainBranch().ShouldBeTrue();
    }

    [Fact]
    public void IsOnMainBranch_OnFeatureBranch_ReturnsFalse()
    {
        var gitDir = Path.Combine(_testDir, ".git");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/feature/loop-command");

        _stateManager.IsOnMainBranch().ShouldBeFalse();
    }
}
