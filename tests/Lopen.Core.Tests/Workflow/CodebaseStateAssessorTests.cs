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
        var assessor = new CodebaseStateAssessor(fs, scanner, ProjectRoot, NullLogger<CodebaseStateAssessor>.Instance);
        return (fs, assessor);
    }

    [Fact]
    public async Task AssessAsync_NoSpec_ReturnsDraftSpecification()
    {
        (InMemoryFileSystem _, CodebaseStateAssessor assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/auth");
        });

        WorkflowAssessment result = await assessor.AssessAsync("auth");
        Assert.Equal(WorkflowStep.DraftSpecification, result.Step);
        Assert.False(result.IsSpecReady);
        Assert.False(result.HasMoreComponents);
    }

    [Fact]
    public async Task AssessAsync_ModuleNotFound_ReturnsDraftSpecification()
    {
        (InMemoryFileSystem _, CodebaseStateAssessor assessor) = CreateAssessor();

        WorkflowAssessment result = await assessor.AssessAsync("nonexistent");
        Assert.Equal(WorkflowStep.DraftSpecification, result.Step);
        Assert.False(result.IsSpecReady);
    }

    [Fact]
    public async Task AssessAsync_AllCheckboxesComplete_ReturnsRepeat_WithNoMoreComponents()
    {
        (InMemoryFileSystem _, CodebaseStateAssessor assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/auth");
            fs.AddFile(ReqDir + "/auth/SPECIFICATION.md",
                "# Auth\n\nSpec content here.\n\n# AC\n\n- [x] First\n- [x] Second");
        });

        WorkflowAssessment result = await assessor.AssessAsync("auth");
        Assert.Equal(WorkflowStep.Repeat, result.Step);
        Assert.True(result.IsSpecReady);
        Assert.False(result.HasMoreComponents);
    }

    [Fact]
    public async Task AssessAsync_SomeCheckboxesComplete_ReturnsIterateThroughTasks()
    {
        (InMemoryFileSystem _, CodebaseStateAssessor assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/core");
            fs.AddFile(ReqDir + "/core/SPECIFICATION.md",
                "# Core\n\nLong spec content that is over one hundred characters for testing purposes.\n\n# AC\n\n- [x] Done\n- [ ] Pending");
        });

        WorkflowAssessment result = await assessor.AssessAsync("core");
        Assert.Equal(WorkflowStep.IterateThroughTasks, result.Step);
        Assert.True(result.IsSpecReady);
        Assert.True(result.HasMoreComponents);
    }

    [Fact]
    public async Task AssessAsync_SpecWithContent_ReturnsDetermineDependencies()
    {
        (InMemoryFileSystem _, CodebaseStateAssessor assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/llm");
            fs.AddFile(ReqDir + "/llm/SPECIFICATION.md",
                "# LLM\n\n" + new string('x', 200) + "\n\n# AC\n\n- [ ] Todo");
        });

        WorkflowAssessment result = await assessor.AssessAsync("llm");
        Assert.Equal(WorkflowStep.DetermineDependencies, result.Step);
        Assert.True(result.IsSpecReady);
    }

    [Fact]
    public async Task RecordStepAsync_WritesHintFile_ThatAssessAsyncReads()
    {
        (InMemoryFileSystem _, CodebaseStateAssessor assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/auth");
            fs.AddFile(ReqDir + "/auth/SPECIFICATION.md", "# Auth\n\nShort");
        });

        await assessor.RecordStepAsync("auth", WorkflowStep.BreakIntoTasks);

        WorkflowAssessment result = await assessor.AssessAsync("auth");
        Assert.Equal(WorkflowStep.BreakIntoTasks, result.Step);
    }

    [Fact]
    public async Task AssessAsync_IgnoresHint_WhenCheckboxesAreTicked()
    {
        (InMemoryFileSystem _, CodebaseStateAssessor assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/auth");
            fs.AddFile(ReqDir + "/auth/SPECIFICATION.md",
                "# Auth\n\n- [x] Done\n- [ ] Pending");
        });

        // Record a hint suggesting an earlier planning step
        await assessor.RecordStepAsync("auth", WorkflowStep.DetermineDependencies);

        // Filesystem truth (some checkboxes ticked) should override the hint
        WorkflowAssessment result = await assessor.AssessAsync("auth");
        Assert.Equal(WorkflowStep.IterateThroughTasks, result.Step);
    }

    [Fact]
    public async Task AssessAsync_ShortSpec_ReturnsDraftSpecification_WhenNoHint()
    {
        (InMemoryFileSystem _, CodebaseStateAssessor assessor) = CreateAssessor(fs =>
        {
            fs.AddDirectory(ReqDir + "/auth");
            fs.AddFile(ReqDir + "/auth/SPECIFICATION.md", "# Auth\n\nShort");
        });

        WorkflowAssessment result = await assessor.AssessAsync("auth");
        Assert.Equal(WorkflowStep.DraftSpecification, result.Step);
    }

    [Fact]
    public void Constructor_NullFileSystem_Throws()
    {
        var fs = new InMemoryFileSystem();
        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, ProjectRoot);
        Assert.Throws<ArgumentNullException>(
            () => new CodebaseStateAssessor(null!, scanner, ProjectRoot, NullLogger<CodebaseStateAssessor>.Instance));
    }

    [Fact]
    public void Constructor_NullScanner_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CodebaseStateAssessor(new InMemoryFileSystem(), null!, ProjectRoot, NullLogger<CodebaseStateAssessor>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var fs = new InMemoryFileSystem();
        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, ProjectRoot);
        Assert.Throws<ArgumentNullException>(
            () => new CodebaseStateAssessor(fs, scanner, ProjectRoot, null!));
    }
}
