using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Workflow;

using Lopen.Core.Workflow;

/// <summary>
/// Tests for ModuleSelectionService. Covers JOB-074 (CORE-24) acceptance criteria.
/// </summary>
public class ModuleSelectionServiceTests
{
    // ==================== Test Helpers ====================

    private static ModuleSelectionService CreateService(
        IReadOnlyList<ModuleState>? modules = null,
        string? promptResponse = null)
    {
        var lister = new StubModuleLister(modules ?? []);
        var renderer = new StubOutputRenderer(promptResponse);
        return new ModuleSelectionService(lister, renderer, NullLogger<ModuleSelectionService>.Instance);
    }

    // ==================== No Modules ====================

    [Fact]
    public async Task SelectModuleAsync_NoModules_ReturnsNull()
    {
        var service = CreateService(modules: []);
        var result = await service.SelectModuleAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task SelectModuleAsync_NoModules_RendersError()
    {
        var renderer = new StubOutputRenderer(null);
        var service = new ModuleSelectionService(
            new StubModuleLister([]),
            renderer,
            NullLogger<ModuleSelectionService>.Instance);

        await service.SelectModuleAsync();
        Assert.Contains(renderer.ErrorMessages, m => m.Contains("No modules found"));
    }

    // ==================== Selection by Number ====================

    [Fact]
    public async Task SelectModuleAsync_SelectByNumber_ReturnsModuleName()
    {
        var modules = new List<ModuleState>
        {
            new("auth", "docs/requirements/auth/SPECIFICATION.md", ModuleStatus.InProgress, 3, 10),
            new("core", "docs/requirements/core/SPECIFICATION.md", ModuleStatus.NotStarted, 0, 20),
        };
        var service = CreateService(modules, promptResponse: "1");
        var result = await service.SelectModuleAsync();
        Assert.Equal("auth", result);
    }

    [Fact]
    public async Task SelectModuleAsync_SelectSecondModule_ReturnsCorrectName()
    {
        var modules = new List<ModuleState>
        {
            new("auth", "path", ModuleStatus.InProgress, 3, 10),
            new("core", "path", ModuleStatus.NotStarted, 0, 20),
        };
        var service = CreateService(modules, promptResponse: "2");
        var result = await service.SelectModuleAsync();
        Assert.Equal("core", result);
    }

    // ==================== Selection by Name ====================

    [Fact]
    public async Task SelectModuleAsync_SelectByName_ReturnsModuleName()
    {
        var modules = new List<ModuleState>
        {
            new("auth", "path", ModuleStatus.InProgress, 3, 10),
            new("core", "path", ModuleStatus.NotStarted, 0, 20),
        };
        var service = CreateService(modules, promptResponse: "core");
        var result = await service.SelectModuleAsync();
        Assert.Equal("core", result);
    }

    [Fact]
    public async Task SelectModuleAsync_SelectByNameCaseInsensitive_ReturnsModuleName()
    {
        var modules = new List<ModuleState>
        {
            new("Auth", "path", ModuleStatus.InProgress, 3, 10),
        };
        var service = CreateService(modules, promptResponse: "auth");
        var result = await service.SelectModuleAsync();
        Assert.Equal("Auth", result);
    }

    // ==================== Invalid Selection ====================

    [Fact]
    public async Task SelectModuleAsync_InvalidNumber_ReturnsNull()
    {
        var modules = new List<ModuleState>
        {
            new("auth", "path", ModuleStatus.InProgress, 3, 10),
        };
        var service = CreateService(modules, promptResponse: "99");
        var result = await service.SelectModuleAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task SelectModuleAsync_InvalidName_ReturnsNull()
    {
        var modules = new List<ModuleState>
        {
            new("auth", "path", ModuleStatus.InProgress, 3, 10),
        };
        var service = CreateService(modules, promptResponse: "nonexistent");
        var result = await service.SelectModuleAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task SelectModuleAsync_ZeroIndex_ReturnsNull()
    {
        var modules = new List<ModuleState>
        {
            new("auth", "path", ModuleStatus.InProgress, 3, 10),
        };
        var service = CreateService(modules, promptResponse: "0");
        var result = await service.SelectModuleAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task SelectModuleAsync_NegativeIndex_ReturnsNull()
    {
        var modules = new List<ModuleState>
        {
            new("auth", "path", ModuleStatus.InProgress, 3, 10),
        };
        var service = CreateService(modules, promptResponse: "-1");
        var result = await service.SelectModuleAsync();
        Assert.Null(result);
    }

    // ==================== Non-Interactive Mode ====================

    [Fact]
    public async Task SelectModuleAsync_NullPromptResponse_ReturnsNull()
    {
        var modules = new List<ModuleState>
        {
            new("auth", "path", ModuleStatus.InProgress, 3, 10),
        };
        var service = CreateService(modules, promptResponse: null);
        var result = await service.SelectModuleAsync();
        Assert.Null(result);
    }

    // ==================== Display Formatting ====================

    [Fact]
    public async Task SelectModuleAsync_DisplaysModuleList()
    {
        var modules = new List<ModuleState>
        {
            new("auth", "path", ModuleStatus.InProgress, 3, 10),
            new("core", "path", ModuleStatus.Complete, 20, 20),
        };
        var renderer = new StubOutputRenderer("1");
        var service = new ModuleSelectionService(
            new StubModuleLister(modules),
            renderer,
            NullLogger<ModuleSelectionService>.Instance);

        await service.SelectModuleAsync();

        Assert.Contains(renderer.ResultMessages, m => m.Contains("auth"));
        Assert.Contains(renderer.ResultMessages, m => m.Contains("core"));
    }

    [Fact]
    public void FormatModuleList_ShowsStatusIndicators()
    {
        var modules = new List<ModuleState>
        {
            new("auth", "path", ModuleStatus.InProgress, 3, 10),
            new("core", "path", ModuleStatus.Complete, 20, 20),
            new("tui", "path", ModuleStatus.NotStarted, 0, 15),
            new("unknown", "path", ModuleStatus.Unknown, 0, 0),
        };

        var formatted = ModuleSelectionService.FormatModuleList(modules);
        Assert.Contains("In Progress", formatted);
        Assert.Contains("Complete", formatted);
        Assert.Contains("Not Started", formatted);
        Assert.Contains("Unknown", formatted);
        Assert.Contains("3/10", formatted);
        Assert.Contains("20/20", formatted);
    }

    [Fact]
    public void FormatModuleList_NumbersModules()
    {
        var modules = new List<ModuleState>
        {
            new("auth", "path", ModuleStatus.InProgress, 3, 10),
            new("core", "path", ModuleStatus.Complete, 20, 20),
        };

        var formatted = ModuleSelectionService.FormatModuleList(modules);
        Assert.Contains("1.", formatted);
        Assert.Contains("2.", formatted);
    }

    // ==================== DI Registration ====================

    [Fact]
    public void AddLopenCore_RegistersModuleSelectionService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Storage.IFileSystem>(new InMemoryFileSystem());
        services.AddSingleton<IOutputRenderer, StubOutputRenderer>();
        services.AddLopenCore("/tmp/test");

        using var sp = services.BuildServiceProvider();
        var selector = sp.GetService<IModuleSelectionService>();
        Assert.NotNull(selector);
    }

    // ==================== Constructor Validation ====================

    [Fact]
    public void Constructor_NullLister_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ModuleSelectionService(null!, new StubOutputRenderer(null),
                NullLogger<ModuleSelectionService>.Instance));
    }

    [Fact]
    public void Constructor_NullRenderer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ModuleSelectionService(new StubModuleLister([]), null!,
                NullLogger<ModuleSelectionService>.Instance));
    }

    // ==================== Test Stubs ====================

    private sealed class StubModuleLister : IModuleLister
    {
        private readonly IReadOnlyList<ModuleState> _modules;
        public StubModuleLister(IReadOnlyList<ModuleState> modules) => _modules = modules;
        public IReadOnlyList<ModuleState> ListModules() => _modules;
    }

    private sealed class StubOutputRenderer : IOutputRenderer
    {
        private readonly string? _promptResponse;
        public List<string> ResultMessages { get; } = [];
        public List<string> ErrorMessages { get; } = [];
        public List<string> PromptMessages { get; } = [];

        public StubOutputRenderer(string? promptResponse = null) => _promptResponse = promptResponse;

        public Task RenderProgressAsync(string phase, string step, double progress, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RenderErrorAsync(string message, Exception? exception = null, CancellationToken ct = default)
        {
            ErrorMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task RenderResultAsync(string message, CancellationToken ct = default)
        {
            ResultMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task<string?> PromptAsync(string message, CancellationToken ct = default)
        {
            PromptMessages.Add(message);
            return Task.FromResult(_promptResponse);
        }
    }
}
