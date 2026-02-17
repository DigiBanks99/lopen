using System.Text.Json;

namespace Lopen.Configuration.Tests;

[Collection("EnvironmentVariableTests")]
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

    // === CLI flag override convenience methods ===

    [Fact]
    public void AddUnattendedOverride_SetsWorkflowUnattended()
    {
        var builder = new LopenConfigurationBuilder();
        builder.AddUnattendedOverride();

        var (options, _) = builder.Build();

        Assert.True(options.Workflow.Unattended);
    }

    [Fact]
    public void AddUnattendedOverride_False_DisablesUnattended()
    {
        var builder = new LopenConfigurationBuilder();
        builder.AddUnattendedOverride(false);

        var (options, _) = builder.Build();

        Assert.False(options.Workflow.Unattended);
    }

    [Fact]
    public void AddResumeOverride_True_EnablesAutoResume()
    {
        var builder = new LopenConfigurationBuilder();
        builder.AddResumeOverride(true);

        var (options, _) = builder.Build();

        Assert.True(options.Session.AutoResume);
    }

    [Fact]
    public void AddResumeOverride_False_DisablesAutoResume()
    {
        var builder = new LopenConfigurationBuilder();
        builder.AddResumeOverride(false);

        var (options, _) = builder.Build();

        Assert.False(options.Session.AutoResume);
    }

    [Fact]
    public void AddMaxIterationsOverride_SetsMaxIterations()
    {
        var builder = new LopenConfigurationBuilder();
        builder.AddMaxIterationsOverride(42);

        var (options, _) = builder.Build();

        Assert.Equal(42, options.Workflow.MaxIterations);
    }

    [Fact]
    public void AddMaxIterationsOverride_OverridesProjectConfig()
    {
        var projectPath = Path.Combine(_tempDir, "project.json");
        File.WriteAllText(projectPath, """{"Workflow": {"MaxIterations": 200}}""");

        var builder = new LopenConfigurationBuilder(projectConfigPath: projectPath);
        builder.AddMaxIterationsOverride(15);

        var (options, _) = builder.Build();

        Assert.Equal(15, options.Workflow.MaxIterations);
    }

    // === Environment variable override tests ===

    private static void WithEnvVars(Dictionary<string, string> vars, Action action)
    {
        foreach (var (key, value) in vars)
            Environment.SetEnvironmentVariable(key, value);
        try
        {
            action();
        }
        finally
        {
            foreach (var key in vars.Keys)
                Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void Build_EnvVarOverride_OverridesProjectConfig()
    {
        var projectPath = Path.Combine(_tempDir, "project.json");
        File.WriteAllText(projectPath, JsonSerializer.Serialize(
            new { Models = new { Planning = "claude-sonnet-4" } }));

        WithEnvVars(new() { ["LOPEN_Models__Planning"] = "gpt-5" }, () =>
        {
            var builder = new LopenConfigurationBuilder(projectConfigPath: projectPath);
            var (options, _) = builder.Build();

            Assert.Equal("gpt-5", options.Models.Planning);
        });
    }

    [Fact]
    public void Build_CliOverride_OverridesEnvVar()
    {
        WithEnvVars(new() { ["LOPEN_Workflow__MaxIterations"] = "50" }, () =>
        {
            var builder = new LopenConfigurationBuilder();
            builder.AddOverride("Workflow:MaxIterations", "10");

            var (options, _) = builder.Build();

            Assert.Equal(10, options.Workflow.MaxIterations);
        });
    }

    [Fact]
    public void Build_EnvVarOverride_OverridesGlobalConfig()
    {
        var globalPath = Path.Combine(_tempDir, "global.json");
        File.WriteAllText(globalPath, JsonSerializer.Serialize(
            new { Workflow = new { Unattended = false } }));

        WithEnvVars(new() { ["LOPEN_Workflow__Unattended"] = "true" }, () =>
        {
            var builder = new LopenConfigurationBuilder(globalConfigPath: globalPath);
            var (options, _) = builder.Build();

            Assert.True(options.Workflow.Unattended);
        });
    }

    [Fact]
    public void Build_EnvVar_NestedKey_SetsNestedProperty()
    {
        WithEnvVars(new() { ["LOPEN_Budget__WarningThreshold"] = "0.5" }, () =>
        {
            var builder = new LopenConfigurationBuilder();
            var (options, _) = builder.Build();

            Assert.Equal(0.5, options.Budget.WarningThreshold);
        });
    }

    [Fact]
    public void Build_FullLayerPrecedence_CliWinsOverEnvOverProjectOverGlobal()
    {
        var globalPath = Path.Combine(_tempDir, "global.json");
        var projectPath = Path.Combine(_tempDir, "project.json");

        File.WriteAllText(globalPath, JsonSerializer.Serialize(
            new { Models = new { Planning = "model-a" } }));
        File.WriteAllText(projectPath, JsonSerializer.Serialize(
            new { Models = new { Planning = "model-b" } }));

        WithEnvVars(new() { ["LOPEN_Models__Planning"] = "model-c" }, () =>
        {
            var builder = new LopenConfigurationBuilder(globalPath, projectPath);
            builder.AddOverride("Models:Planning", "model-d");

            var (options, _) = builder.Build();

            Assert.Equal("model-d", options.Models.Planning);
        });
    }
}
