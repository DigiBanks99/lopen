using Lopen.Llm.Tools;
using Microsoft.Extensions.AI;

namespace Lopen.Llm.Tests.Tools;

public sealed class ToolCatalogTests
{
    private readonly ToolCatalog _catalog;

    public ToolCatalogTests()
    {
        var fs = new FakeFileSystem();
        var extractor = new NoOpSectionExtractor();
        var engine = new StubWorkflowEngine();
        var tracker = new StubVerificationTracker();

        _catalog = new ToolCatalog(
            fs,
            extractor,
            engine,
            tracker,
            "/project",
            taskStatusGate: null,
            planManager: null,
            oracleVerifier: null);
    }

    [Fact]
    public void GetToolsForPhase_RequirementGathering_ReturnsCorrectTools()
    {
        var tools = _catalog.GetToolsForPhase(WorkflowPhase.RequirementGathering);

        var names = tools.Select(t => t.Name).ToList();
        Assert.Equal(5, names.Count);
        Assert.Contains("read_spec", names);
        Assert.Contains("read_research", names);
        Assert.Contains("get_current_context", names);
        Assert.Contains("log_research", names);
        Assert.Contains("report_progress", names);
    }

    [Fact]
    public void GetToolsForPhase_Planning_ReturnsCorrectTools()
    {
        var tools = _catalog.GetToolsForPhase(WorkflowPhase.Planning);

        var names = tools.Select(t => t.Name).ToList();
        Assert.Equal(6, names.Count);
        Assert.Contains("read_spec", names);
        Assert.Contains("read_research", names);
        Assert.Contains("read_plan", names);
        Assert.Contains("get_current_context", names);
        Assert.Contains("log_research", names);
        Assert.Contains("report_progress", names);
    }

    [Fact]
    public void GetToolsForPhase_Building_ReturnsCorrectTools()
    {
        var tools = _catalog.GetToolsForPhase(WorkflowPhase.Building);

        var names = tools.Select(t => t.Name).ToList();
        Assert.Equal(7, names.Count);
        Assert.Contains("read_plan", names);
        Assert.Contains("update_task_status", names);
        Assert.Contains("get_current_context", names);
        Assert.Contains("report_progress", names);
        Assert.Contains("verify_task_completion", names);
        Assert.Contains("verify_component_completion", names);
        Assert.Contains("verify_module_completion", names);
    }

    [Fact]
    public void GetToolsForPhase_Research_ReturnsCorrectTools()
    {
        var tools = _catalog.GetToolsForPhase(WorkflowPhase.Research);

        var names = tools.Select(t => t.Name).ToList();
        Assert.Equal(5, names.Count);
        Assert.Contains("read_spec", names);
        Assert.Contains("read_research", names);
        Assert.Contains("get_current_context", names);
        Assert.Contains("log_research", names);
        Assert.Contains("report_progress", names);
    }

    #region Stubs

    private sealed class NoOpSectionExtractor : IToolSectionExtractor
    {
        public IReadOnlyList<ToolExtractedSection> ExtractRelevantSections(
            string content, IReadOnlyList<string> headers) =>
            Array.Empty<ToolExtractedSection>();
    }

    private sealed class StubWorkflowEngine : IToolWorkflowEngine
    {
        public string CurrentStep => "stub";
        public WorkflowPhase CurrentPhase => WorkflowPhase.Building;
        public bool IsComplete => false;
        public IReadOnlyList<string> GetPermittedTriggers() => [];
    }

    private sealed class StubVerificationTracker : IVerificationTracker
    {
        public void RecordVerification(VerificationScope scope, string identifier, bool passed) { }
        public bool IsVerified(VerificationScope scope, string identifier) => false;
        public void ResetForInvocation() { }
    }

    #endregion
}
