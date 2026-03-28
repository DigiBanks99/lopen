using Lopen.Llm.Tools;
using Lopen.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Llm.Tests;

public sealed class DefaultPromptBuilderTests
{
    private readonly ToolCatalog _toolCatalog;
    private readonly DefaultPromptBuilder _builder;

    public DefaultPromptBuilderTests()
    {
        _toolCatalog = new ToolCatalog(
            new StubFileSystem(),
            new StubToolSectionExtractor(),
            new StubToolWorkflowEngine(),
            new StubVerificationTracker(),
            "/stub-root");
        _builder = new DefaultPromptBuilder(_toolCatalog, NullLogger<DefaultPromptBuilder>.Instance);
    }

    [Fact]
    public void BuildSystemPrompt_ContainsRoleSection()
    {
        var prompt = _builder.BuildSystemPrompt(WorkflowPhase.Building, "auth", null, null);

        Assert.Contains("# Role", prompt);
        Assert.Contains("Lopen", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ContainsWorkflowState()
    {
        var prompt = _builder.BuildSystemPrompt(WorkflowPhase.Planning, "auth", "jwt", "parse-token");

        Assert.Contains("# Workflow State", prompt);
        Assert.Contains("**Phase**: Planning", prompt);
        Assert.Contains("**Module**: auth", prompt);
        Assert.Contains("**Component**: jwt", prompt);
        Assert.Contains("**Task**: parse-token", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_OmitsComponentAndTask_WhenNull()
    {
        var prompt = _builder.BuildSystemPrompt(WorkflowPhase.Research, "storage", null, null);

        Assert.DoesNotContain("**Component**", prompt);
        Assert.DoesNotContain("**Task**", prompt);
    }

    [Theory]
    [InlineData(WorkflowPhase.RequirementGathering, "Gather and refine requirements")]
    [InlineData(WorkflowPhase.Planning, "Plan the implementation")]
    [InlineData(WorkflowPhase.Building, "Implement the current task")]
    [InlineData(WorkflowPhase.Research, "Research the topic")]
    public void BuildSystemPrompt_ContainsPhaseSpecificInstructions(WorkflowPhase phase, string expectedFragment)
    {
        var prompt = _builder.BuildSystemPrompt(phase, "core", null, null);

        Assert.Contains("# Instructions", prompt);
        Assert.Contains(expectedFragment, prompt);
    }

    [Fact]
    public void BuildSystemPrompt_IncludesContextSections()
    {
        var sections = new Dictionary<string, string>
        {
            ["Auth Spec §JWT"] = "JWT tokens must be validated with HMAC-SHA256.",
            ["Research Notes"] = "The SDK supports automatic token refresh.",
        };

        var prompt = _builder.BuildSystemPrompt(WorkflowPhase.Building, "auth", "jwt", "validate", sections);

        Assert.Contains("# Context", prompt);
        Assert.Contains("## Auth Spec §JWT", prompt);
        Assert.Contains("JWT tokens must be validated with HMAC-SHA256.", prompt);
        Assert.Contains("## Research Notes", prompt);
        Assert.Contains("The SDK supports automatic token refresh.", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_OmitsContextSection_WhenNoSections()
    {
        var prompt = _builder.BuildSystemPrompt(WorkflowPhase.Building, "auth", null, null);

        Assert.DoesNotContain("# Context", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ContainsToolsSection()
    {
        var prompt = _builder.BuildSystemPrompt(WorkflowPhase.Building, "auth", null, null);

        Assert.Contains("# Available Tools", prompt);
        Assert.Contains("**update_task_status**", prompt);
        Assert.Contains("**read_plan**", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ContainsConstraints()
    {
        var prompt = _builder.BuildSystemPrompt(WorkflowPhase.Building, "auth", null, null);

        Assert.Contains("# Constraints", prompt);
        Assert.Contains("conventional commit", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_SectionsInCorrectOrder()
    {
        var sections = new Dictionary<string, string> { ["TestSection"] = "content" };
        var prompt = _builder.BuildSystemPrompt(WorkflowPhase.Building, "auth", "c", "t", sections);

        var roleIdx = prompt.IndexOf("# Role", StringComparison.Ordinal);
        var stateIdx = prompt.IndexOf("# Workflow State", StringComparison.Ordinal);
        var instrIdx = prompt.IndexOf("# Instructions", StringComparison.Ordinal);
        var ctxIdx = prompt.IndexOf("# Context", StringComparison.Ordinal);
        var toolsIdx = prompt.IndexOf("# Available Tools", StringComparison.Ordinal);
        var constraintIdx = prompt.IndexOf("# Constraints", StringComparison.Ordinal);

        Assert.True(roleIdx < stateIdx);
        Assert.True(stateIdx < instrIdx);
        Assert.True(instrIdx < ctxIdx);
        Assert.True(ctxIdx < toolsIdx);
        Assert.True(toolsIdx < constraintIdx);
    }

    [Fact]
    public void BuildSystemPrompt_NullModule_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => _builder.BuildSystemPrompt(WorkflowPhase.Building, null!, null, null));
    }

    [Fact]
    public void BuildSystemPrompt_EmptyModule_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => _builder.BuildSystemPrompt(WorkflowPhase.Building, "", null, null));
    }

    [Fact]
    public void Constructor_NullToolCatalog_DoesNotThrow()
    {
        var builder = new DefaultPromptBuilder(null, NullLogger<DefaultPromptBuilder>.Instance);
        Assert.NotNull(builder);
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new DefaultPromptBuilder(_toolCatalog, null!));
    }

    private sealed class StubFileSystem : IFileSystem
    {
        public void CreateDirectory(string path) { }
        public bool FileExists(string path) => false;
        public bool DirectoryExists(string path) => false;
        public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default) => Task.FromResult("");
        public Task WriteAllTextAsync(string path, string content, CancellationToken ct = default) => Task.CompletedTask;
        public IEnumerable<string> GetFiles(string path, string searchPattern = "*") => [];
        public IEnumerable<string> GetDirectories(string path) => [];
        public void MoveFile(string source, string dest) { }
        public void DeleteFile(string path) { }
        public void DeleteDirectory(string path, bool recursive = true) { }
        public void CreateSymlink(string linkPath, string targetPath) { }
        public string? GetSymlinkTarget(string linkPath) => null;
        public DateTime GetLastWriteTimeUtc(string path) => DateTime.UtcNow;
    }

    private sealed class StubToolSectionExtractor : IToolSectionExtractor
    {
        public IReadOnlyList<ToolExtractedSection> ExtractRelevantSections(string content, IReadOnlyList<string> headers) => [];
    }

    private sealed class StubToolWorkflowEngine : IToolWorkflowEngine
    {
        public string CurrentStep => "DraftSpecification";
        public WorkflowPhase CurrentPhase => WorkflowPhase.RequirementGathering;
        public bool IsComplete => false;
        public IReadOnlyList<string> GetPermittedTriggers() => [];
    }

    private sealed class StubVerificationTracker : IVerificationTracker
    {
        public void RecordVerification(VerificationScope scope, string identifier, bool passed) { }
        public bool IsVerified(VerificationScope scope, string identifier) => false;
        public void ResetForInvocation() { }
    }
}
