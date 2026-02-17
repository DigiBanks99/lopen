using Microsoft.Extensions.Configuration;

namespace Lopen.Configuration;

/// <summary>
/// Builds a layered <see cref="IConfigurationRoot"/> and binds it to <see cref="LopenOptions"/>.
/// Resolution order (highest priority last): built-in defaults → global config → project config → env vars → overrides dictionary.
/// </summary>
public sealed class LopenConfigurationBuilder
{
    private readonly string? _globalConfigPath;
    private readonly string? _projectConfigPath;
    private readonly Dictionary<string, string?> _overrides = new();

    public LopenConfigurationBuilder(string? globalConfigPath = null, string? projectConfigPath = null)
    {
        _globalConfigPath = globalConfigPath;
        _projectConfigPath = projectConfigPath;
    }

    /// <summary>
    /// Adds a CLI flag override. Keys use the configuration path format (e.g., "Models:Planning").
    /// </summary>
    public LopenConfigurationBuilder AddOverride(string key, string value)
    {
        _overrides[key] = value;
        return this;
    }

    /// <summary>
    /// Applies a --model flag that overrides all model phase assignments.
    /// </summary>
    public LopenConfigurationBuilder AddModelOverride(string model)
    {
        _overrides["Models:RequirementGathering"] = model;
        _overrides["Models:Planning"] = model;
        _overrides["Models:Building"] = model;
        _overrides["Models:Research"] = model;
        return this;
    }

    /// <summary>
    /// Applies the --unattended flag override.
    /// </summary>
    public LopenConfigurationBuilder AddUnattendedOverride(bool unattended = true)
    {
        _overrides["Workflow:Unattended"] = unattended.ToString();
        return this;
    }

    /// <summary>
    /// Applies the --resume or --no-resume flag override.
    /// </summary>
    public LopenConfigurationBuilder AddResumeOverride(bool autoResume)
    {
        _overrides["Session:AutoResume"] = autoResume.ToString();
        return this;
    }

    /// <summary>
    /// Applies the --max-iterations flag override.
    /// </summary>
    public LopenConfigurationBuilder AddMaxIterationsOverride(int maxIterations)
    {
        _overrides["Workflow:MaxIterations"] = maxIterations.ToString();
        return this;
    }

    /// <summary>
    /// Builds the layered configuration and binds to <see cref="LopenOptions"/>.
    /// Validates and throws <see cref="InvalidOperationException"/> if validation fails.
    /// </summary>
    public (LopenOptions Options, IConfigurationRoot Configuration) Build()
    {
        var configBuilder = new ConfigurationBuilder();

        // Layer 1: Global configuration
        if (_globalConfigPath is not null && File.Exists(_globalConfigPath))
        {
            configBuilder.AddJsonFile(_globalConfigPath, optional: true, reloadOnChange: false);
        }

        // Layer 2: Project configuration
        if (_projectConfigPath is not null && File.Exists(_projectConfigPath))
        {
            configBuilder.AddJsonFile(_projectConfigPath, optional: true, reloadOnChange: false);
        }

        // Layer 3: Environment variables (prefixed with LOPEN_)
        configBuilder.AddEnvironmentVariables("LOPEN_");

        // Layer 4: CLI flag overrides (highest priority)
        if (_overrides.Count > 0)
        {
            configBuilder.AddInMemoryCollection(_overrides);
        }

        var configRoot = configBuilder.Build();

        var options = new LopenOptions();
        configRoot.Bind(options);

        var errors = LopenOptionsValidator.Validate(options);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Configuration validation failed:\n" + string.Join("\n", errors.Select(e => $"  - {e}")));
        }

        return (options, configRoot);
    }

    /// <summary>
    /// Resolves the global configuration file path.
    /// </summary>
    public static string GetDefaultGlobalConfigPath()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(configHome, "lopen", "config.json");
    }

    /// <summary>
    /// Discovers the project configuration file by searching for .lopen/config.json
    /// in the current directory or nearest parent.
    /// </summary>
    public static string? DiscoverProjectConfigPath(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ".lopen", "config.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
