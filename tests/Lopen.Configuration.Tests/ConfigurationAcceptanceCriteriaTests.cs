using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Configuration.Tests;

/// <summary>
/// Explicit acceptance criteria coverage tests for the Configuration module.
/// Each test maps to a numbered AC from docs/requirements/configuration/SPECIFICATION.md (CFG-01 through CFG-16).
/// </summary>
[Collection("EnvironmentVariableTests")]
public class ConfigurationAcceptanceCriteriaTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigurationAcceptanceCriteriaTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lopen-cfg-ac-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteJsonFile(string filename, object config)
    {
        var path = Path.Combine(_tempDir, filename);
        File.WriteAllText(path, JsonSerializer.Serialize(config));
        return path;
    }

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

    // CFG-01: Configuration hierarchy resolves in order: CLI flags → environment variables → project config → global config → built-in defaults

    [Fact]
    public void AC01_Hierarchy_ResolvesInCorrectPriorityOrder()
    {
        var globalPath = WriteJsonFile("global.json",
            new { Models = new { Planning = "global-model" }, Workflow = new { MaxIterations = 50 } });
        var projectPath = WriteJsonFile("project.json",
            new { Models = new { Planning = "project-model" } });

        WithEnvVars(new() { ["LOPEN_Workflow__MaxIterations"] = "75" }, () =>
        {
            var builder = new LopenConfigurationBuilder(globalPath, projectPath);
            builder.AddOverride("Models:Planning", "cli-model");

            var (options, _) = builder.Build();

            // CLI wins over env, project, global
            Assert.Equal("cli-model", options.Models.Planning);
            // Env wins over project (project didn't set it) and global
            Assert.Equal(75, options.Workflow.MaxIterations);
        });
    }

    [Fact]
    public void AC01_Hierarchy_DefaultsUsedWhenNoOtherSourceProvides()
    {
        var builder = new LopenConfigurationBuilder(
            globalConfigPath: Path.Combine(_tempDir, "nonexistent-global.json"),
            projectConfigPath: Path.Combine(_tempDir, "nonexistent-project.json"));

        var (options, _) = builder.Build();

        Assert.Equal("claude-opus-4.6", options.Models.Planning);
        Assert.Equal(100, options.Workflow.MaxIterations);
        Assert.True(options.Session.AutoResume);
    }

    // CFG-02: Higher-priority source wins when a setting is defined at multiple levels

    [Fact]
    public void AC02_HigherPriority_WinsWhenDefinedAtMultipleLevels()
    {
        var globalPath = WriteJsonFile("global.json",
            new { Models = new { Planning = "model-a" } });
        var projectPath = WriteJsonFile("project.json",
            new { Models = new { Planning = "model-b" } });

        WithEnvVars(new() { ["LOPEN_Models__Planning"] = "model-c" }, () =>
        {
            var builder = new LopenConfigurationBuilder(globalPath, projectPath);
            builder.AddOverride("Models:Planning", "model-d");

            var (options, _) = builder.Build();

            Assert.Equal("model-d", options.Models.Planning);
        });
    }

    [Fact]
    public void AC02_HigherPriority_EnvWinsOverProjectAndGlobal()
    {
        var globalPath = WriteJsonFile("global.json",
            new { Models = new { Building = "global-builder" } });
        var projectPath = WriteJsonFile("project.json",
            new { Models = new { Building = "project-builder" } });

        WithEnvVars(new() { ["LOPEN_Models__Building"] = "env-builder" }, () =>
        {
            var builder = new LopenConfigurationBuilder(globalPath, projectPath);
            var (options, _) = builder.Build();

            Assert.Equal("env-builder", options.Models.Building);
        });
    }

    [Fact]
    public void AC02_HigherPriority_ProjectWinsOverGlobal()
    {
        var globalPath = WriteJsonFile("global.json",
            new { Workflow = new { MaxIterations = 50 } });
        var projectPath = WriteJsonFile("project.json",
            new { Workflow = new { MaxIterations = 25 } });

        var builder = new LopenConfigurationBuilder(globalPath, projectPath);
        var (options, _) = builder.Build();

        Assert.Equal(25, options.Workflow.MaxIterations);
    }

    // CFG-03: Project configuration is discovered at .lopen/config.json in the current working directory or nearest parent with .lopen/

    [Fact]
    public void AC03_ProjectConfig_DiscoveredInCurrentDirectory()
    {
        var projectDir = Path.Combine(_tempDir, "myproject");
        var lopenDir = Path.Combine(projectDir, ".lopen");
        Directory.CreateDirectory(lopenDir);
        File.WriteAllText(Path.Combine(lopenDir, "config.json"), "{}");

        var result = LopenConfigurationBuilder.DiscoverProjectConfigPath(projectDir);

        Assert.NotNull(result);
        Assert.Equal(Path.Combine(lopenDir, "config.json"), result);
    }

    [Fact]
    public void AC03_ProjectConfig_DiscoveredInNearestParent()
    {
        var projectDir = Path.Combine(_tempDir, "repo");
        var lopenDir = Path.Combine(projectDir, ".lopen");
        var deepChild = Path.Combine(projectDir, "src", "modules", "auth");
        Directory.CreateDirectory(lopenDir);
        Directory.CreateDirectory(deepChild);
        File.WriteAllText(Path.Combine(lopenDir, "config.json"), "{}");

        var result = LopenConfigurationBuilder.DiscoverProjectConfigPath(deepChild);

        Assert.NotNull(result);
        Assert.Equal(Path.Combine(lopenDir, "config.json"), result);
    }

    [Fact]
    public void AC03_ProjectConfig_ReturnsNullWhenNotFound()
    {
        var emptyDir = Path.Combine(_tempDir, "noconfig");
        Directory.CreateDirectory(emptyDir);

        var result = LopenConfigurationBuilder.DiscoverProjectConfigPath(emptyDir);

        Assert.Null(result);
    }

    // CFG-04: Global configuration is discovered at ~/.config/lopen/config.json

    [Fact]
    public void AC04_GlobalConfig_DefaultPathContainsExpectedSegments()
    {
        var path = LopenConfigurationBuilder.GetDefaultGlobalConfigPath();

        Assert.Contains("lopen", path);
        Assert.EndsWith("config.json", path);
    }

    [Fact]
    public void AC04_GlobalConfig_LoadedWhenFileExists()
    {
        var globalPath = WriteJsonFile("global.json",
            new { Oracle = new { Model = "custom-oracle" } });

        var builder = new LopenConfigurationBuilder(globalConfigPath: globalPath);
        var (options, _) = builder.Build();

        Assert.Equal("custom-oracle", options.Oracle.Model);
    }

    // CFG-05: lopen config show displays the resolved configuration with sources indicated for each setting

    [Fact]
    public void AC05_ConfigShow_DisplaysResolvedSettingsWithSources()
    {
        var globalPath = WriteJsonFile("global.json",
            new { Models = new { Planning = "gpt-5" } });

        var builder = new LopenConfigurationBuilder(globalConfigPath: globalPath);
        var (_, configRoot) = builder.Build();

        var entries = ConfigurationDiagnostics.GetEntries(configRoot);

        Assert.NotEmpty(entries);
        var planningEntry = entries.FirstOrDefault(e => e.Key == "Models:Planning");
        Assert.NotNull(planningEntry);
        Assert.Equal("gpt-5", planningEntry.Value);
        Assert.NotEmpty(planningEntry.Source);
    }

    [Fact]
    public void AC05_ConfigShow_FormatProducesReadableTable()
    {
        var globalPath = WriteJsonFile("global.json",
            new { Models = new { Planning = "gpt-5" } });

        var builder = new LopenConfigurationBuilder(globalConfigPath: globalPath);
        var (_, configRoot) = builder.Build();

        var entries = ConfigurationDiagnostics.GetEntries(configRoot);
        var formatted = ConfigurationDiagnostics.Format(entries);

        Assert.Contains("Setting", formatted);
        Assert.Contains("Value", formatted);
        Assert.Contains("Source", formatted);
    }

    // CFG-06: lopen config show --json outputs machine-readable JSON

    [Fact]
    public void AC06_ConfigShowJson_OutputsMachineReadableJson()
    {
        var projectPath = WriteJsonFile("project.json",
            new { Workflow = new { MaxIterations = 42 } });

        var builder = new LopenConfigurationBuilder(projectConfigPath: projectPath);
        var (_, configRoot) = builder.Build();

        var entries = ConfigurationDiagnostics.GetEntries(configRoot);
        var json = ConfigurationDiagnostics.FormatJson(entries);

        Assert.StartsWith("[", json.TrimStart());
        Assert.Contains("\"key\"", json);
        Assert.Contains("\"value\"", json);
        Assert.Contains("\"source\"", json);
    }

    [Fact]
    public void AC06_ConfigShowJson_ContainsConfiguredValues()
    {
        var projectPath = WriteJsonFile("project.json",
            new { Workflow = new { MaxIterations = 42 } });

        var builder = new LopenConfigurationBuilder(projectConfigPath: projectPath);
        var (_, configRoot) = builder.Build();

        var entries = ConfigurationDiagnostics.GetEntries(configRoot);
        var json = ConfigurationDiagnostics.FormatJson(entries);

        Assert.Contains("42", json);
    }

    // CFG-07: All settings have sensible built-in defaults — Lopen works without any configuration files

    [Fact]
    public void AC07_Defaults_AllSettingsHaveSensibleValues()
    {
        var builder = new LopenConfigurationBuilder();
        var (options, _) = builder.Build();

        // Model defaults
        Assert.Equal("claude-opus-4.6", options.Models.RequirementGathering);
        Assert.Equal("claude-opus-4.6", options.Models.Planning);
        Assert.Equal("claude-opus-4.6", options.Models.Building);
        Assert.Equal("claude-opus-4.6", options.Models.Research);

        // Budget defaults (zero = unlimited)
        Assert.Equal(0, options.Budget.TokenBudgetPerModule);
        Assert.Equal(0, options.Budget.PremiumRequestBudget);
        Assert.Equal(0.8, options.Budget.WarningThreshold);
        Assert.Equal(0.9, options.Budget.ConfirmationThreshold);

        // Oracle default
        Assert.Equal("gpt-5-mini", options.Oracle.Model);

        // Workflow defaults
        Assert.False(options.Workflow.Unattended);
        Assert.Equal(100, options.Workflow.MaxIterations);
        Assert.Equal(3, options.Workflow.FailureThreshold);

        // Session defaults
        Assert.True(options.Session.AutoResume);
        Assert.Equal(10, options.Session.SessionRetention);
        Assert.False(options.Session.SaveIterationHistory);

        // Git defaults
        Assert.True(options.Git.Enabled);
        Assert.True(options.Git.AutoCommit);
        Assert.Equal("conventional", options.Git.Convention);

        // Tool discipline defaults
        Assert.Equal(3, options.ToolDiscipline.MaxFileReads);
        Assert.Equal(3, options.ToolDiscipline.MaxCommandRetries);

        // Display defaults
        Assert.True(options.Display.ShowTokenUsage);
        Assert.True(options.Display.ShowPremiumCount);
    }

    [Fact]
    public void AC07_Defaults_ValidationPassesWithNoConfigFiles()
    {
        var builder = new LopenConfigurationBuilder();

        var exception = Record.Exception(() => builder.Build());

        Assert.Null(exception);
    }

    // CFG-08: --model <name> CLI flag overrides all model phase assignments for the invocation

    [Fact]
    public void AC08_ModelFlag_OverridesAllPhaseAssignments()
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
    public void AC08_ModelFlag_OverridesProjectConfigModels()
    {
        var projectPath = WriteJsonFile("project.json", new
        {
            Models = new
            {
                RequirementGathering = "project-rg",
                Planning = "project-plan",
                Building = "project-build",
                Research = "project-research"
            }
        });

        var builder = new LopenConfigurationBuilder(projectConfigPath: projectPath);
        builder.AddModelOverride("cli-override-model");

        var (options, _) = builder.Build();

        Assert.Equal("cli-override-model", options.Models.RequirementGathering);
        Assert.Equal("cli-override-model", options.Models.Planning);
        Assert.Equal("cli-override-model", options.Models.Building);
        Assert.Equal("cli-override-model", options.Models.Research);
    }

    // CFG-09: --unattended CLI flag overrides the unattended setting

    [Fact]
    public void AC09_UnattendedFlag_OverridesUnattendedSetting()
    {
        var builder = new LopenConfigurationBuilder();
        builder.AddUnattendedOverride();

        var (options, _) = builder.Build();

        Assert.True(options.Workflow.Unattended);
    }

    [Fact]
    public void AC09_UnattendedFlag_OverridesProjectConfig()
    {
        var projectPath = WriteJsonFile("project.json",
            new { Workflow = new { Unattended = false } });

        var builder = new LopenConfigurationBuilder(projectConfigPath: projectPath);
        builder.AddUnattendedOverride(true);

        var (options, _) = builder.Build();

        Assert.True(options.Workflow.Unattended);
    }

    [Fact]
    public void AC09_UnattendedFlag_FalseDisablesUnattended()
    {
        var projectPath = WriteJsonFile("project.json",
            new { Workflow = new { Unattended = true } });

        var builder = new LopenConfigurationBuilder(projectConfigPath: projectPath);
        builder.AddUnattendedOverride(false);

        var (options, _) = builder.Build();

        Assert.False(options.Workflow.Unattended);
    }

    // CFG-10: --resume <id> and --no-resume CLI flags override auto_resume behavior

    [Fact]
    public void AC10_ResumeFlag_EnablesAutoResume()
    {
        var projectPath = WriteJsonFile("project.json",
            new { Session = new { AutoResume = false } });

        var builder = new LopenConfigurationBuilder(projectConfigPath: projectPath);
        builder.AddResumeOverride(true);

        var (options, _) = builder.Build();

        Assert.True(options.Session.AutoResume);
    }

    [Fact]
    public void AC10_NoResumeFlag_DisablesAutoResume()
    {
        var builder = new LopenConfigurationBuilder();
        builder.AddResumeOverride(false);

        var (options, _) = builder.Build();

        Assert.False(options.Session.AutoResume);
    }

    // CFG-11: --max-iterations <n> CLI flag overrides max_iterations

    [Fact]
    public void AC11_MaxIterationsFlag_OverridesMaxIterations()
    {
        var builder = new LopenConfigurationBuilder();
        builder.AddMaxIterationsOverride(42);

        var (options, _) = builder.Build();

        Assert.Equal(42, options.Workflow.MaxIterations);
    }

    [Fact]
    public void AC11_MaxIterationsFlag_OverridesProjectConfig()
    {
        var projectPath = WriteJsonFile("project.json",
            new { Workflow = new { MaxIterations = 200 } });

        var builder = new LopenConfigurationBuilder(projectConfigPath: projectPath);
        builder.AddMaxIterationsOverride(15);

        var (options, _) = builder.Build();

        Assert.Equal(15, options.Workflow.MaxIterations);
    }

    // CFG-12: Budget settings (token_budget_per_module, premium_request_budget) are respected when non-zero

    [Fact]
    public void AC12_BudgetSettings_TokenBudgetEnforcedWhenNonZero()
    {
        var budget = new BudgetOptions
        {
            TokenBudgetPerModule = 10000,
            PremiumRequestBudget = 0,
            WarningThreshold = 0.8,
            ConfirmationThreshold = 0.9
        };
        var enforcer = new BudgetEnforcer(budget);

        var ok = enforcer.Check(5000, 0);
        Assert.Equal(BudgetStatus.Ok, ok.Status);
        Assert.NotNull(ok.TokenUsageFraction);
        Assert.Equal(0.5, ok.TokenUsageFraction!.Value, precision: 2);

        var warning = enforcer.Check(8500, 0);
        Assert.Equal(BudgetStatus.Warning, warning.Status);

        var confirmation = enforcer.Check(9500, 0);
        Assert.Equal(BudgetStatus.ConfirmationRequired, confirmation.Status);

        var exceeded = enforcer.Check(10000, 0);
        Assert.Equal(BudgetStatus.Exceeded, exceeded.Status);
    }

    [Fact]
    public void AC12_BudgetSettings_PremiumBudgetEnforcedWhenNonZero()
    {
        var budget = new BudgetOptions
        {
            TokenBudgetPerModule = 0,
            PremiumRequestBudget = 100,
            WarningThreshold = 0.8,
            ConfirmationThreshold = 0.9
        };
        var enforcer = new BudgetEnforcer(budget);

        var ok = enforcer.Check(0, 50);
        Assert.Equal(BudgetStatus.Ok, ok.Status);
        Assert.NotNull(ok.RequestUsageFraction);

        var warning = enforcer.Check(0, 85);
        Assert.Equal(BudgetStatus.Warning, warning.Status);

        var exceeded = enforcer.Check(0, 100);
        Assert.Equal(BudgetStatus.Exceeded, exceeded.Status);
    }

    [Fact]
    public void AC12_BudgetSettings_ZeroBudgetMeansUnlimited()
    {
        var budget = new BudgetOptions
        {
            TokenBudgetPerModule = 0,
            PremiumRequestBudget = 0,
            WarningThreshold = 0.8,
            ConfirmationThreshold = 0.9
        };
        var enforcer = new BudgetEnforcer(budget);

        var result = enforcer.Check(999999, 999);

        Assert.Equal(BudgetStatus.Ok, result.Status);
        Assert.Null(result.TokenUsageFraction);
        Assert.Null(result.RequestUsageFraction);
    }

    // CFG-13: Oracle model setting is passed to the LLM module for verification sub-agent dispatch

    [Fact]
    public void AC13_OracleModel_ResolvedThroughDI()
    {
        var options = new LopenOptions();
        options.Oracle.Model = "custom-oracle-model";

        var services = new ServiceCollection();
        services.AddLopenConfiguration(options);
        var provider = services.BuildServiceProvider();

        var oracleOptions = provider.GetRequiredService<OracleOptions>();

        Assert.Equal("custom-oracle-model", oracleOptions.Model);
    }

    [Fact]
    public void AC13_OracleModel_DefaultIsGpt5Mini()
    {
        var options = new LopenOptions();

        Assert.Equal("gpt-5-mini", options.Oracle.Model);
    }

    // CFG-14: Tool discipline settings control corrective injection thresholds

    [Fact]
    public void AC14_ToolDiscipline_ResolvedThroughDI()
    {
        var options = new LopenOptions();
        options.ToolDiscipline.MaxFileReads = 5;
        options.ToolDiscipline.MaxCommandRetries = 7;

        var services = new ServiceCollection();
        services.AddLopenConfiguration(options);
        var provider = services.BuildServiceProvider();

        var toolOptions = provider.GetRequiredService<ToolDisciplineOptions>();

        Assert.Equal(5, toolOptions.MaxFileReads);
        Assert.Equal(7, toolOptions.MaxCommandRetries);
    }

    [Fact]
    public void AC14_ToolDiscipline_DefaultsAreCorrect()
    {
        var options = new LopenOptions();

        Assert.Equal(3, options.ToolDiscipline.MaxFileReads);
        Assert.Equal(3, options.ToolDiscipline.MaxCommandRetries);
    }

    [Fact]
    public void AC14_ToolDiscipline_ConfigFileOverridesDefaults()
    {
        var projectPath = WriteJsonFile("project.json",
            new { ToolDiscipline = new { MaxFileReads = 10, MaxCommandRetries = 5 } });

        var builder = new LopenConfigurationBuilder(projectConfigPath: projectPath);
        var (options, _) = builder.Build();

        Assert.Equal(10, options.ToolDiscipline.MaxFileReads);
        Assert.Equal(5, options.ToolDiscipline.MaxCommandRetries);
    }

    // CFG-15: Invalid configuration values produce clear error messages with guidance

    [Fact]
    public void AC15_InvalidConfig_MaxIterationsZero_ProducesGuidance()
    {
        var options = new LopenOptions();
        options.Workflow.MaxIterations = 0;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("max_iterations"));
    }

    [Fact]
    public void AC15_InvalidConfig_NegativeTokenBudget_ProducesGuidance()
    {
        var options = new LopenOptions();
        options.Budget.TokenBudgetPerModule = -1;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("token_budget_per_module"));
    }

    [Fact]
    public void AC15_InvalidConfig_ThresholdOutOfRange_ProducesGuidance()
    {
        var options = new LopenOptions();
        options.Budget.WarningThreshold = 1.5;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("warning_threshold"));
    }

    [Fact]
    public void AC15_InvalidConfig_WarningAboveConfirmation_ProducesGuidance()
    {
        var options = new LopenOptions();
        options.Budget.WarningThreshold = 0.95;
        options.Budget.ConfirmationThreshold = 0.9;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("warning_threshold") && e.Contains("confirmation_threshold"));
    }

    [Fact]
    public void AC15_InvalidConfig_NegativeSessionRetention_ProducesGuidance()
    {
        var options = new LopenOptions();
        options.Session.SessionRetention = -1;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("session_retention"));
    }

    [Fact]
    public void AC15_InvalidConfig_ZeroToolDiscipline_ProducesGuidance()
    {
        var options = new LopenOptions();
        options.ToolDiscipline.MaxFileReads = 0;
        options.ToolDiscipline.MaxCommandRetries = 0;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.True(errors.Count >= 2);
        Assert.Contains(errors, e => e.Contains("max_file_reads"));
        Assert.Contains(errors, e => e.Contains("max_command_retries"));
    }

    [Fact]
    public void AC15_InvalidConfig_MultipleErrors_AllReported()
    {
        var options = new LopenOptions();
        options.Workflow.MaxIterations = 0;
        options.Workflow.FailureThreshold = 0;
        options.Budget.TokenBudgetPerModule = -1;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.True(errors.Count >= 3);
    }

    [Fact]
    public void AC15_InvalidConfig_BuilderThrowsWithAggregatedErrors()
    {
        var builder = new LopenConfigurationBuilder();
        builder.AddOverride("Workflow:MaxIterations", "0");
        builder.AddOverride("Workflow:FailureThreshold", "0");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("max_iterations", ex.Message);
        Assert.Contains("failure_threshold", ex.Message);
    }

    // CFG-16: LOPEN_-prefixed environment variables override file-based config but are overridden by CLI flags

    [Fact]
    public void AC16_EnvVars_OverrideGlobalConfig()
    {
        var globalPath = WriteJsonFile("global.json",
            new { Models = new { Planning = "global-model" } });

        WithEnvVars(new() { ["LOPEN_Models__Planning"] = "env-model" }, () =>
        {
            var builder = new LopenConfigurationBuilder(globalConfigPath: globalPath);
            var (options, _) = builder.Build();

            Assert.Equal("env-model", options.Models.Planning);
        });
    }

    [Fact]
    public void AC16_EnvVars_OverrideProjectConfig()
    {
        var projectPath = WriteJsonFile("project.json",
            new { Models = new { Building = "project-builder" } });

        WithEnvVars(new() { ["LOPEN_Models__Building"] = "env-builder" }, () =>
        {
            var builder = new LopenConfigurationBuilder(projectConfigPath: projectPath);
            var (options, _) = builder.Build();

            Assert.Equal("env-builder", options.Models.Building);
        });
    }

    [Fact]
    public void AC16_EnvVars_OverriddenByCliFlags()
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
    public void AC16_EnvVars_NestedKeysResolveCorrectly()
    {
        WithEnvVars(new() { ["LOPEN_Budget__WarningThreshold"] = "0.5" }, () =>
        {
            var builder = new LopenConfigurationBuilder();
            var (options, _) = builder.Build();

            Assert.Equal(0.5, options.Budget.WarningThreshold);
        });
    }

    [Fact]
    public void AC16_EnvVars_FullLayerPrecedence_CliWinsOverEnvOverProjectOverGlobal()
    {
        var globalPath = WriteJsonFile("global.json",
            new { Models = new { Research = "model-global" } });
        var projectPath = WriteJsonFile("project.json",
            new { Models = new { Research = "model-project" } });

        WithEnvVars(new() { ["LOPEN_Models__Research"] = "model-env" }, () =>
        {
            var builder = new LopenConfigurationBuilder(globalPath, projectPath);
            builder.AddOverride("Models:Research", "model-cli");

            var (options, _) = builder.Build();

            Assert.Equal("model-cli", options.Models.Research);
        });
    }
}
