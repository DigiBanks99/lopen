using System.Text.Json;

namespace Lopen.Configuration.Tests;

public class LopenConfigurationBuilderTests : IDisposable
{
    private readonly string _tempDir;

    public LopenConfigurationBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lopen-config-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Build_NoConfigFiles_ReturnsDefaults()
    {
        var builder = new LopenConfigurationBuilder(
            globalConfigPath: Path.Combine(_tempDir, "nonexistent-global.json"),
            projectConfigPath: Path.Combine(_tempDir, "nonexistent-project.json"));

        var (options, _) = builder.Build();

        Assert.Equal("claude-opus-4.6", options.Models.Planning);
        Assert.Equal(100, options.Workflow.MaxIterations);
        Assert.True(options.Session.AutoResume);
    }

    [Fact]
    public void Build_GlobalConfig_OverridesDefaults()
    {
        var globalPath = Path.Combine(_tempDir, "global.json");
        var config = new { Models = new { Planning = "gpt-5" } };
        File.WriteAllText(globalPath, JsonSerializer.Serialize(config));

        var builder = new LopenConfigurationBuilder(globalConfigPath: globalPath);

        var (options, _) = builder.Build();

        Assert.Equal("gpt-5", options.Models.Planning);
        // Other defaults remain
        Assert.Equal("claude-opus-4.6", options.Models.Building);
    }

    [Fact]
    public void Build_ProjectConfig_OverridesGlobalConfig()
    {
        var globalPath = Path.Combine(_tempDir, "global.json");
        var projectPath = Path.Combine(_tempDir, "project.json");

        File.WriteAllText(globalPath, JsonSerializer.Serialize(
            new { Workflow = new { MaxIterations = 50 } }));
        File.WriteAllText(projectPath, JsonSerializer.Serialize(
            new { Workflow = new { MaxIterations = 25 } }));

        var builder = new LopenConfigurationBuilder(globalPath, projectPath);

        var (options, _) = builder.Build();

        Assert.Equal(25, options.Workflow.MaxIterations);
    }

    [Fact]
    public void Build_CliOverrides_OverrideProjectConfig()
    {
        var projectPath = Path.Combine(_tempDir, "project.json");
        File.WriteAllText(projectPath, JsonSerializer.Serialize(
            new { Workflow = new { MaxIterations = 25 } }));

        var builder = new LopenConfigurationBuilder(projectConfigPath: projectPath);
        builder.AddOverride("Workflow:MaxIterations", "10");

        var (options, _) = builder.Build();

        Assert.Equal(10, options.Workflow.MaxIterations);
    }

    [Fact]
    public void Build_ModelOverride_SetsAllPhases()
    {
        var builder = new LopenConfigurationBuilder();
        builder.AddModelOverride("gpt-5");

        var (options, _) = builder.Build();

        Assert.Equal("gpt-5", options.Models.RequirementGathering);
        Assert.Equal("gpt-5", options.Models.Planning);
        Assert.Equal("gpt-5", options.Models.Building);
        Assert.Equal("gpt-5", options.Models.Research);
    }

    [Fact]
    public void Build_InvalidConfig_ThrowsWithAggregatedErrors()
    {
        var builder = new LopenConfigurationBuilder();
        builder.AddOverride("Workflow:MaxIterations", "0");
        builder.AddOverride("Workflow:FailureThreshold", "0");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("max_iterations", ex.Message);
        Assert.Contains("failure_threshold", ex.Message);
    }

    [Fact]
    public void Build_LayeredResolution_HighestPriorityWins()
    {
        var globalPath = Path.Combine(_tempDir, "global.json");
        var projectPath = Path.Combine(_tempDir, "project.json");

        // Global sets model to gpt-5 and max_iterations to 50
        File.WriteAllText(globalPath, JsonSerializer.Serialize(
            new { Models = new { Planning = "gpt-5" }, Workflow = new { MaxIterations = 50 } }));

        // Project overrides model to claude-sonnet but keeps max_iterations from global
        File.WriteAllText(projectPath, JsonSerializer.Serialize(
            new { Models = new { Planning = "claude-sonnet-4" } }));

        // CLI overrides max_iterations
        var builder = new LopenConfigurationBuilder(globalPath, projectPath);
        builder.AddOverride("Workflow:MaxIterations", "10");

        var (options, _) = builder.Build();

        Assert.Equal("claude-sonnet-4", options.Models.Planning);
        Assert.Equal(10, options.Workflow.MaxIterations);
    }

    [Fact]
    public void DiscoverProjectConfigPath_FindsConfigInParent()
    {
        var projectDir = Path.Combine(_tempDir, "project");
        var lopenDir = Path.Combine(projectDir, ".lopen");
        var childDir = Path.Combine(projectDir, "src", "module");
        Directory.CreateDirectory(lopenDir);
        Directory.CreateDirectory(childDir);
        File.WriteAllText(Path.Combine(lopenDir, "config.json"), "{}");

        var result = LopenConfigurationBuilder.DiscoverProjectConfigPath(childDir);

        Assert.NotNull(result);
        Assert.Equal(Path.Combine(lopenDir, "config.json"), result);
    }

    [Fact]
    public void DiscoverProjectConfigPath_ReturnsNull_WhenNoConfigExists()
    {
        var dir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(dir);

        var result = LopenConfigurationBuilder.DiscoverProjectConfigPath(dir);

        Assert.Null(result);
    }

    [Fact]
    public void GetDefaultGlobalConfigPath_ReturnsExpectedPath()
    {
        var path = LopenConfigurationBuilder.GetDefaultGlobalConfigPath();

        Assert.Contains("lopen", path);
        Assert.EndsWith("config.json", path);
    }
}
