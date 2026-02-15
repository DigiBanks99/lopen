using Lopen.Core.Workflow;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Workflow;

public sealed class CodebaseStateAssessorTests
{
    private const string ProjectRoot = "/project";
    private const string ReqDir = "/project/docs/requirements";

    private static (InMemoryFileSystem fs, CodebaseStateAssessor assessor) CreateAssessor(
        Action<InMemoryFileSystem>? setup = null)
    {
        var fs = new InMemoryFileSystem();
        fs.AddDirectory(ReqDir);
        setup?.Invoke(fs);

        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, ProjectRoot);
        var assessor = new CodebaseStateAssessor(fs, scanner, NullLogger<CodebaseStateAssessor>.Instance);
        return (fs, assessor);
    }

    [Fact]
    public async Task GetCurrentStep_NoSpec_ReturnsDraftSpecification()
    {
        var (_, assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/auth");
        });

        var step = await assessor.GetCurrentStepAsync("auth");
        Assert.Equal(WorkflowStep.DraftSpecification, step);
    }

    [Fact]
    public async Task GetCurrentStep_ModuleNotFound_ReturnsDraftSpecification()
    {
        var (_, assessor) = CreateAssessor();

        var step = await assessor.GetCurrentStepAsync("nonexistent");
        Assert.Equal(WorkflowStep.DraftSpecification, step);
    }

    [Fact]
    public async Task GetCurrentStep_AllCheckboxesComplete_ReturnsRepeat()
    {
        var (_, assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/auth");
            fs.AddFile(ReqDir + "/auth/SPECIFICATION.md",
                "# Auth\n\nSpec content here.\n\n# AC\n\n- [x] First\n- [x] Second");
        });

        var step = await assessor.GetCurrentStepAsync("auth");
        Assert.Equal(WorkflowStep.Repeat, step);
    }

    [Fact]
    public async Task GetCurrentStep_SomeCheckboxesComplete_ReturnsIterate()
    {
        var (_, assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/core");
            fs.AddFile(ReqDir + "/core/SPECIFICATION.md",
                "# Core\n\nLong spec content that is over one hundred characters for testing purposes.\n\n# AC\n\n- [x] Done\n- [ ] Pending");
        });

        var step = await assessor.GetCurrentStepAsync("core");
        Assert.Equal(WorkflowStep.IterateThroughTasks, step);
    }

    [Fact]
    public async Task GetCurrentStep_SpecExistsWithContent_ReturnsDependencies()
    {
        var (_, assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/llm");
            fs.AddFile(ReqDir + "/llm/SPECIFICATION.md",
                "# LLM\n\n" + new string('x', 200) + "\n\n# AC\n\n- [ ] Todo");
        });

        // Has spec with content but no completed checkboxes → DetermineDependencies
        var step = await assessor.GetCurrentStepAsync("llm");
        Assert.Equal(WorkflowStep.DetermineDependencies, step);
    }

    [Fact]
    public async Task GetCurrentStep_PersistedStep_ReturnsPersistedValue()
    {
        var (_, assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/auth");
            fs.AddFile(ReqDir + "/auth/SPECIFICATION.md", "# Auth\n\nShort");
        });

        // Persist a step
        await assessor.PersistStepAsync("auth", WorkflowStep.BreakIntoTasks);

        var step = await assessor.GetCurrentStepAsync("auth");
        Assert.Equal(WorkflowStep.BreakIntoTasks, step);
    }

    [Fact]
    public async Task PersistStep_StoresStep()
    {
        var (_, assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/m");
            fs.AddFile(ReqDir + "/m/SPECIFICATION.md", "# M\n\nShort");
        });

        await assessor.PersistStepAsync("m", WorkflowStep.SelectNextComponent);
        var step = await assessor.GetCurrentStepAsync("m");
        Assert.Equal(WorkflowStep.SelectNextComponent, step);
    }

    [Fact]
    public async Task IsSpecReady_NoSpec_ReturnsFalse()
    {
        var (_, assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/auth");
        });

        Assert.False(await assessor.IsSpecReadyAsync("auth"));
    }

    [Fact]
    public async Task IsSpecReady_WithSpec_ReturnsTrue()
    {
        var (_, assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/auth");
            fs.AddFile(ReqDir + "/auth/SPECIFICATION.md", "# Auth");
        });

        Assert.True(await assessor.IsSpecReadyAsync("auth"));
    }

    [Fact]
    public async Task HasMoreComponents_AllComplete_ReturnsFalse()
    {
        var (_, assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/auth");
            fs.AddFile(ReqDir + "/auth/SPECIFICATION.md",
                "# AC\n\n- [x] Done\n- [x] Done too");
        });

        Assert.False(await assessor.HasMoreComponentsAsync("auth"));
    }

    [Fact]
    public async Task HasMoreComponents_SomePending_ReturnsTrue()
    {
        var (_, assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/core");
            fs.AddFile(ReqDir + "/core/SPECIFICATION.md",
                "# AC\n\n- [x] Done\n- [ ] Pending");
        });

        Assert.True(await assessor.HasMoreComponentsAsync("core"));
    }

    [Fact]
    public async Task HasMoreComponents_NoSpec_ReturnsFalse()
    {
        var (_, assessor) = CreateAssessor();

        Assert.False(await assessor.HasMoreComponentsAsync("missing"));
    }

    [Fact]
    public void Constructor_NullFileSystem_Throws()
    {
        var fs = new InMemoryFileSystem();
        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, ProjectRoot);
        Assert.Throws<ArgumentNullException>(
            () => new CodebaseStateAssessor(null!, scanner, NullLogger<CodebaseStateAssessor>.Instance));
    }

    [Fact]
    public void Constructor_NullScanner_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CodebaseStateAssessor(new InMemoryFileSystem(), null!, NullLogger<CodebaseStateAssessor>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var fs = new InMemoryFileSystem();
        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, ProjectRoot);
        Assert.Throws<ArgumentNullException>(
            () => new CodebaseStateAssessor(fs, scanner, null!));
    }
}
