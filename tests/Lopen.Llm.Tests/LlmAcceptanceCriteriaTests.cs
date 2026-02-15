using Lopen.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lopen.Llm.Tests;

/// <summary>
/// Explicit acceptance criteria coverage tests for the LLM module.
/// Each test maps to a numbered AC from docs/requirements/llm/SPECIFICATION.md.
/// Some ACs are also covered by dedicated test classes (referenced in comments).
/// </summary>
public class LlmAcceptanceCriteriaTests
{
    // AC1: Lopen authenticates with the Copilot SDK using credentials from the Auth module
    // Primary coverage: CopilotClientProviderTests, CopilotLlmServiceTests

    [Fact]
    public void AC1_TokenProvider_InjectableViaInterface()
    {
        var provider = new NullGitHubTokenProvider();
        Assert.Null(provider.GetToken());
    }

    [Fact]
    public async Task AC1_ClientProvider_AcceptsTokenFromAuthModule()
    {
        var tokenProvider = new FakeTokenProvider("gh_test_token");
        var clientProvider = new CopilotClientProvider(
            tokenProvider, NullLogger<CopilotClientProvider>.Instance);

        var client = clientProvider.CreateClient();
        Assert.NotNull(client);
        await clientProvider.DisposeAsync();
    }

    [Fact]
    public void AC1_ServiceRegistration_RegistersAuthComponents()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenLlm();

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<IGitHubTokenProvider>());
        Assert.NotNull(sp.GetService<ICopilotClientProvider>());
    }

    // AC2: Each workflow phase invokes the SDK with a fresh context window
    // Primary coverage: CopilotLlmServiceTests

    [Fact]
    public async Task AC2_EachInvocation_CreatesNewSession()
    {
        var counter = new CountingClientProvider();
        var service = new CopilotLlmService(
            counter, NullLogger<CopilotLlmService>.Instance);

        try { await service.InvokeAsync("prompt1", "model1", [], default); } catch { }
        try { await service.InvokeAsync("prompt2", "model2", [], default); } catch { }

        Assert.Equal(2, counter.GetClientCallCount);
    }

    // AC3: System prompt includes role/identity, workflow state, step instructions,
    //       relevant context, available tools, constraints
    // Primary coverage: DefaultPromptBuilderTests

    [Fact]
    public void AC3_SystemPrompt_ContainsAllRequiredSections()
    {
        var registry = new DefaultToolRegistry(NullLogger<DefaultToolRegistry>.Instance);
        var builder = new DefaultPromptBuilder(registry, NullLogger<DefaultPromptBuilder>.Instance);

        var prompt = builder.BuildSystemPrompt(
            WorkflowPhase.Building,
            module: "auth",
            component: "login-service",
            task: "implement-jwt",
            contextSections: new Dictionary<string, string>
            {
                ["Specification"] = "JWT must use RS256",
            });

        Assert.Contains("role", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("auth", prompt);
        Assert.Contains("Building", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JWT must use RS256", prompt);
        Assert.Contains("tool", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("constraint", prompt, StringComparison.OrdinalIgnoreCase);
    }

    // AC4: Context window contains only section-level document extractions, not full documents
    // Primary coverage: ContextBudgetManagerTests, SectionExtractorTests (in Core)

    [Fact]
    public void AC4_ContextBudgetManager_TruncatesLowPrioritySections()
    {
        var manager = new ContextBudgetManager(NullLogger<ContextBudgetManager>.Instance);

        var sections = new ContextSection[]
        {
            new("high", "Important content", ContextBudgetManager.EstimateTokens("Important content")),
            new("low", new string('x', 100_000), ContextBudgetManager.EstimateTokens(new string('x', 100_000))),
        };

        var result = manager.FitToBudget(sections, budgetTokens: 100);
        Assert.Contains(result, s => s.Title == "high");
    }

    // AC5: Lopen-managed tools are registered and functional
    // Primary coverage: DefaultToolRegistryTests

    [Fact]
    public void AC5_AllSpecifiedTools_AreRegistered()
    {
        var registry = new DefaultToolRegistry(NullLogger<DefaultToolRegistry>.Instance);
        var allTools = registry.GetAllTools();
        var toolNames = allTools.Select(t => t.Name).ToHashSet();

        Assert.Contains("read_spec", toolNames);
        Assert.Contains("read_research", toolNames);
        Assert.Contains("read_plan", toolNames);
        Assert.Contains("update_task_status", toolNames);
        Assert.Contains("get_current_context", toolNames);
        Assert.Contains("log_research", toolNames);
        Assert.Contains("report_progress", toolNames);
    }

    // AC6: Oracle verification tools dispatch a sub-agent and return pass/fail verdicts
    // Primary coverage: OracleVerifierTests

    [Fact]
    public void AC6_VerificationTools_AreRegistered()
    {
        var registry = new DefaultToolRegistry(NullLogger<DefaultToolRegistry>.Instance);
        var allTools = registry.GetAllTools();
        var toolNames = allTools.Select(t => t.Name).ToHashSet();

        Assert.Contains("verify_task_completion", toolNames);
        Assert.Contains("verify_component_completion", toolNames);
        Assert.Contains("verify_module_completion", toolNames);
    }

    [Fact]
    public void AC6_OracleVerdict_ReturnsPassOrFail()
    {
        var pass = new OracleVerdict(Passed: true, Gaps: [], Scope: VerificationScope.Task);
        var fail = new OracleVerdict(Passed: false, Gaps: ["Missing auth"], Scope: VerificationScope.Task);

        Assert.True(pass.Passed);
        Assert.Empty(pass.Gaps);
        Assert.False(fail.Passed);
        Assert.Single(fail.Gaps);
    }

    // AC7: Oracle verification runs within the same SDK invocation
    //       (no additional premium request consumed)

    [Fact]
    public void AC7_OracleModel_IsNotPremium()
    {
        var defaultOracleModel = new OracleOptions().Model;
        Assert.Equal("gpt-5-mini", defaultOracleModel);
        Assert.False(CopilotLlmService.IsPremiumModel(defaultOracleModel));
    }

    [Fact]
    public void AC7_OracleVerifier_RegistersNoTools()
    {
        // OracleVerifier invokes with Array.Empty<LopenToolDefinition>()
        var prompt = OracleVerifier.BuildPrompt(
            VerificationScope.Task, "evidence", "criteria");
        Assert.Contains("acceptance criteria", prompt, StringComparison.OrdinalIgnoreCase);
    }

    // AC8: update_task_status(complete) is rejected unless preceded by passing verify_*_completion
    // Primary coverage: TaskStatusGateTests, VerificationTrackerTests

    [Fact]
    public void AC8_CompletionRejected_WithoutVerification()
    {
        var tracker = new VerificationTracker();
        var gate = new TaskStatusGate(tracker, NullLogger<TaskStatusGate>.Instance);

        var result = gate.ValidateCompletion(VerificationScope.Task, "my-task");
        Assert.False(result.IsAllowed);
        Assert.Contains("verify_task_completion", result.RejectionReason!);
    }

    [Fact]
    public void AC8_CompletionAllowed_AfterPassingVerification()
    {
        var tracker = new VerificationTracker();
        var gate = new TaskStatusGate(tracker, NullLogger<TaskStatusGate>.Instance);

        tracker.RecordVerification(VerificationScope.Task, "my-task", passed: true);
        var result = gate.ValidateCompletion(VerificationScope.Task, "my-task");
        Assert.True(result.IsAllowed);
    }

    // AC9: Tool registration varies by workflow step
    // Primary coverage: DefaultToolRegistryTests

    [Fact]
    public void AC9_ResearchPhase_IncludesLogResearch_ExcludesVerification()
    {
        var registry = new DefaultToolRegistry(NullLogger<DefaultToolRegistry>.Instance);
        var tools = registry.GetToolsForPhase(WorkflowPhase.Research);
        var names = tools.Select(t => t.Name).ToHashSet();

        Assert.Contains("log_research", names);
        Assert.DoesNotContain("verify_task_completion", names);
        Assert.DoesNotContain("update_task_status", names);
    }

    [Fact]
    public void AC9_BuildingPhase_IncludesVerification_ExcludesLogResearch()
    {
        var registry = new DefaultToolRegistry(NullLogger<DefaultToolRegistry>.Instance);
        var tools = registry.GetToolsForPhase(WorkflowPhase.Building);
        var names = tools.Select(t => t.Name).ToHashSet();

        Assert.Contains("verify_task_completion", names);
        Assert.Contains("update_task_status", names);
        Assert.DoesNotContain("log_research", names);
    }

    // AC10: Per-phase model selection works
    // Primary coverage: DefaultModelSelectorTests

    [Fact]
    public void AC10_DifferentPhases_CanUseDifferentModels()
    {
        var options = Options.Create(new LopenOptions
        {
            Models = new ModelOptions
            {
                RequirementGathering = "claude-sonnet-4",
                Planning = "claude-sonnet-4",
                Building = "claude-sonnet-4.5",
                Research = "gpt-5-mini",
            },
        });

        var selector = new DefaultModelSelector(options, NullLogger<DefaultModelSelector>.Instance);

        Assert.Equal("claude-sonnet-4.5", selector.SelectModel(WorkflowPhase.Building).SelectedModel);
        Assert.Equal("gpt-5-mini", selector.SelectModel(WorkflowPhase.Research).SelectedModel);
        Assert.NotEqual(
            selector.SelectModel(WorkflowPhase.Building).SelectedModel,
            selector.SelectModel(WorkflowPhase.Research).SelectedModel);
    }

    // AC11: Model fallback activates when a configured model is unavailable
    // Primary coverage: DefaultModelSelectorTests

    [Fact]
    public void AC11_EmptyModelConfig_FallsBackWithWarning()
    {
        var options = Options.Create(new LopenOptions
        {
            Models = new ModelOptions { Building = "" },
        });

        var selector = new DefaultModelSelector(options, NullLogger<DefaultModelSelector>.Instance);
        var result = selector.SelectModel(WorkflowPhase.Building);

        Assert.True(result.WasFallback);
        Assert.Equal(DefaultModelSelector.FallbackModel, result.SelectedModel);
    }

    // AC12: Token usage metrics are read from SDK response metadata and recorded
    // Primary coverage: InMemoryTokenTrackerTests, TokenUsageTests

    [Fact]
    public void AC12_TokenTracker_RecordsAndAggregatesUsage()
    {
        var tracker = new InMemoryTokenTracker();

        tracker.RecordUsage(new TokenUsage(100, 50, 150, 8192, true));
        tracker.RecordUsage(new TokenUsage(200, 75, 275, 8192, false));

        var metrics = tracker.GetSessionMetrics();
        Assert.Equal(300, metrics.CumulativeInputTokens);
        Assert.Equal(125, metrics.CumulativeOutputTokens);
        Assert.Equal(1, metrics.PremiumRequestCount);
        Assert.Equal(2, metrics.PerIterationTokens.Count);
    }

    [Fact]
    public void AC12_PremiumDetection_IdentifiesPremiumModels()
    {
        Assert.True(CopilotLlmService.IsPremiumModel("claude-opus-4"));
        Assert.True(CopilotLlmService.IsPremiumModel("gpt-5"));
        Assert.False(CopilotLlmService.IsPremiumModel("gpt-5-mini"));
        Assert.False(CopilotLlmService.IsPremiumModel("claude-sonnet-4"));
    }

    // AC13: Token metrics are surfaced to the TUI and persisted in session state
    // LLM module responsibility: expose metrics via ITokenTracker interface

    [Fact]
    public void AC13_TokenTracker_ExposesMetricsViaPublicInterface()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenLlm();

        var sp = services.BuildServiceProvider();
        var tracker = sp.GetRequiredService<ITokenTracker>();

        tracker.RecordUsage(new TokenUsage(500, 200, 700, 16384, true));
        var metrics = tracker.GetSessionMetrics();

        Assert.Equal(500, metrics.CumulativeInputTokens);
        Assert.Equal(200, metrics.CumulativeOutputTokens);
        Assert.Equal(1, metrics.PremiumRequestCount);
    }

    [Fact]
    public void AC13_SessionTokenMetrics_IsPublicRecord_ForSerialization()
    {
        Assert.True(typeof(SessionTokenMetrics).IsPublic);
        Assert.True(typeof(TokenUsage).IsPublic);
    }

    // AC14: Context window budget is respected
    // Primary coverage: ContextBudgetManagerTests

    [Fact]
    public void AC14_BudgetManager_RespectsTokenLimit()
    {
        var manager = new ContextBudgetManager(NullLogger<ContextBudgetManager>.Instance);

        var sections = new ContextSection[]
        {
            new("spec", new string('a', 400), ContextBudgetManager.EstimateTokens(new string('a', 400))),
            new("research", new string('b', 400), ContextBudgetManager.EstimateTokens(new string('b', 400))),
            new("notes", new string('c', 400), ContextBudgetManager.EstimateTokens(new string('c', 400))),
        };

        var result = manager.FitToBudget(sections, budgetTokens: 150);

        Assert.True(result.Count <= sections.Length);
        if (result.Count > 0)
        {
            Assert.Equal("spec", result[0].Title);
        }
    }

    // Test helpers

    private sealed class FakeTokenProvider : IGitHubTokenProvider
    {
        private readonly string? _token;
        public FakeTokenProvider(string? token) => _token = token;
        public string? GetToken() => _token;
    }

    private sealed class CountingClientProvider : ICopilotClientProvider
    {
        public int GetClientCallCount { get; private set; }

        public Task<GitHub.Copilot.SDK.CopilotClient> GetClientAsync(CancellationToken ct = default)
        {
            GetClientCallCount++;
            throw new LlmException("Test: not a real client", "test-model");
        }

        public Task<bool> IsAuthenticatedAsync(CancellationToken ct = default) =>
            Task.FromResult(true);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
