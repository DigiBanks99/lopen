using Lopen.Core.BackPressure;
using Lopen.Core.Documents;
using Lopen.Core.Git;
using Lopen.Core.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lopen.Core.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLopenCore_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddLopenCore();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddLopenCore_RegistersSpecificationParser()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ISpecificationParser>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_RegistersContentHasher()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IContentHasher>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_RegistersGuardrailPipeline()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IGuardrailPipeline>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_RegistersSingletons()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var parser1 = provider.GetService<ISpecificationParser>();
        var parser2 = provider.GetService<ISpecificationParser>();

        Assert.Same(parser1, parser2);
    }

    [Fact]
    public void AddLopenCore_WithProjectRoot_RegistersGitService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Lopen.Storage.IFileSystem, StubFileSystem>();
        services.AddLopenCore(projectRoot: "/tmp");

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IGitService>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_WithProjectRoot_RegistersModuleScanner()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Lopen.Storage.IFileSystem, StubFileSystem>();
        services.AddLopenCore(projectRoot: "/tmp");

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IModuleScanner>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_RegistersOutputRenderer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IOutputRenderer>();

        Assert.NotNull(service);
        Assert.IsType<HeadlessRenderer>(service);
    }

    [Fact]
    public void AddLopenCore_RegistersPhaseTransitionController()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IPhaseTransitionController>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_WithProjectRoot_RegistersWorkflowEngine()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Lopen.Storage.IFileSystem, StubFileSystem>();
        services.AddLopenCore(projectRoot: "/tmp");

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IWorkflowEngine>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_WithProjectRoot_RegistersStateAssessor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Lopen.Storage.IFileSystem, StubFileSystem>();
        services.AddLopenCore(projectRoot: "/tmp");

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IStateAssessor>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_WithProjectRoot_RegistersWorkflowOrchestrator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Lopen.Storage.IFileSystem, StubFileSystem>();
        services.AddSingleton<Lopen.Llm.ILlmService, NullLlmService>();
        services.AddSingleton<Lopen.Llm.IPromptBuilder, NullPromptBuilder>();
        services.AddSingleton<Lopen.Llm.IToolRegistry, NullToolRegistry>();
        services.AddSingleton<Lopen.Llm.IModelSelector, NullModelSelector>();
        services.AddLopenCore(projectRoot: "/tmp");

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IWorkflowOrchestrator>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_WorkflowEngine_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Lopen.Storage.IFileSystem, StubFileSystem>();
        services.AddLopenCore(projectRoot: "/tmp");

        var provider = services.BuildServiceProvider();
        var engine1 = provider.GetService<IWorkflowEngine>();
        var engine2 = provider.GetService<IWorkflowEngine>();

        Assert.Same(engine1, engine2);
    }

    [Fact]
    public void AddLopenCore_WithoutProjectRoot_DoesNotRegisterWorkflowEngine()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IWorkflowEngine>();

        Assert.Null(service);
    }

    [Fact]
    public void AddLopenCore_WithoutProjectRoot_DoesNotRegisterStateAssessor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IStateAssessor>();

        Assert.Null(service);
    }

    [Fact]
    public void AddLopenCore_WithProjectRoot_RegistersFailureHandler()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Lopen.Storage.IFileSystem, StubFileSystem>();
        services.AddLopenCore(projectRoot: "/tmp");

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IFailureHandler>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_WithProjectRoot_FailureHandlerUsesWorkflowOptionsThreshold()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Lopen.Storage.IFileSystem, StubFileSystem>();
        services.AddSingleton(new Lopen.Configuration.WorkflowOptions { FailureThreshold = 5 });
        services.AddLopenCore(projectRoot: "/tmp");

        var provider = services.BuildServiceProvider();
        var handler = provider.GetService<IFailureHandler>();

        Assert.NotNull(handler);
        // Verify threshold by failing 4 times (should still self-correct) then 5th escalates
        for (var i = 0; i < 4; i++)
        {
            var classification = handler.RecordFailure("test-task", "error");
            Assert.Equal(FailureAction.SelfCorrect, classification.Action);
        }
        var fifth = handler.RecordFailure("test-task", "error");
        Assert.Equal(FailureAction.PromptUser, fifth.Action);
    }

    [Fact]
    public void AddLopenCore_WithProjectRoot_OrchestratorResolves_WithBudgetEnforcer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Lopen.Storage.IFileSystem, StubFileSystem>();
        services.AddSingleton<Lopen.Llm.ILlmService, NullLlmService>();
        services.AddSingleton<Lopen.Llm.IPromptBuilder, NullPromptBuilder>();
        services.AddSingleton<Lopen.Llm.IToolRegistry, NullToolRegistry>();
        services.AddSingleton<Lopen.Llm.IModelSelector, NullModelSelector>();
        services.AddSingleton<Lopen.Configuration.IBudgetEnforcer>(
            new Lopen.Configuration.BudgetEnforcer(new Lopen.Configuration.BudgetOptions()));
        services.AddLopenCore(projectRoot: "/tmp");

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IWorkflowOrchestrator>();

        Assert.NotNull(service);
    }

    private sealed class StubFileSystem : Lopen.Storage.IFileSystem
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

    private sealed class NullLlmService : Lopen.Llm.ILlmService
    {
        public Task<Lopen.Llm.LlmInvocationResult> InvokeAsync(string systemPrompt, string model,
            IReadOnlyList<Lopen.Llm.LopenToolDefinition> tools, CancellationToken ct = default) =>
            Task.FromResult(new Lopen.Llm.LlmInvocationResult("", new Lopen.Llm.TokenUsage(0, 0, 0, 0, false), 0, true));
    }

    private sealed class NullPromptBuilder : Lopen.Llm.IPromptBuilder
    {
        public string BuildSystemPrompt(Lopen.Llm.WorkflowPhase phase, string module, string? component, string? task,
            IReadOnlyDictionary<string, string>? contextSections = null) => "";
    }

    private sealed class NullToolRegistry : Lopen.Llm.IToolRegistry
    {
        public IReadOnlyList<Lopen.Llm.LopenToolDefinition> GetToolsForPhase(Lopen.Llm.WorkflowPhase phase) => [];
        public void RegisterTool(Lopen.Llm.LopenToolDefinition tool) { }
        public IReadOnlyList<Lopen.Llm.LopenToolDefinition> GetAllTools() => [];
        public bool BindHandler(string toolName, Func<string, CancellationToken, Task<string>> handler) => true;
    }

    private sealed class NullModelSelector : Lopen.Llm.IModelSelector
    {
        public Lopen.Llm.ModelFallbackResult SelectModel(Lopen.Llm.WorkflowPhase phase) =>
            new("gpt-4", false);

        public IReadOnlyList<string> GetFallbackChain(Lopen.Llm.WorkflowPhase phase) =>
            ["gpt-4"];
    }
}
