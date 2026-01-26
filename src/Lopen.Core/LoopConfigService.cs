using System.Text.Json;

namespace Lopen.Core;

/// <summary>
/// Service for loading and saving loop configuration.
/// </summary>
public class LoopConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _userConfigPath;
    private readonly string _projectConfigPath;

    /// <summary>
    /// Creates a new LoopConfigService with default paths.
    /// </summary>
    public LoopConfigService()
        : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lopen", "loop-config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), ".lopen", "loop-config.json"))
    {
    }

    /// <summary>
    /// Creates a new LoopConfigService with custom paths.
    /// </summary>
    public LoopConfigService(string userConfigPath, string projectConfigPath)
    {
        _userConfigPath = userConfigPath;
        _projectConfigPath = projectConfigPath;
    }

    /// <summary>
    /// Load configuration from user and project config files, merging them.
    /// Project config overrides user config.
    /// </summary>
    public async Task<LoopConfig> LoadConfigAsync(string? customConfigPath = null, CancellationToken ct = default)
    {
        // Start with defaults
        var config = new LoopConfig();

        // Load user config
        var userConfig = await LoadFromFileAsync(_userConfigPath, ct);
        if (userConfig is not null)
        {
            config = config.MergeWith(userConfig);
        }

        // Load project config (overrides user)
        var projectConfig = await LoadFromFileAsync(_projectConfigPath, ct);
        if (projectConfig is not null)
        {
            config = config.MergeWith(projectConfig);
        }

        // Load custom config (overrides all)
        if (!string.IsNullOrEmpty(customConfigPath))
        {
            var customConfig = await LoadFromFileAsync(customConfigPath, ct);
            if (customConfig is not null)
            {
                config = config.MergeWith(customConfig);
            }
        }

        return config;
    }

    /// <summary>
    /// Save configuration to the user config file.
    /// </summary>
    public async Task SaveUserConfigAsync(LoopConfig config, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(_userConfigPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(_userConfigPath, json, ct);
    }

    /// <summary>
    /// Save configuration to the project config file.
    /// </summary>
    public async Task SaveProjectConfigAsync(LoopConfig config, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(_projectConfigPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(_projectConfigPath, json, ct);
    }

    /// <summary>
    /// Reset user configuration to defaults.
    /// </summary>
    public Task ResetUserConfigAsync(CancellationToken ct = default)
    {
        if (File.Exists(_userConfigPath))
        {
            File.Delete(_userConfigPath);
        }
        return Task.CompletedTask;
    }

    private static async Task<LoopConfig?> LoadFromFileAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<LoopConfig>(json, JsonOptions);
        }
        catch (JsonException)
        {
            // Invalid JSON, ignore
            return null;
        }
    }
}
