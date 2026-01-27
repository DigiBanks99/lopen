using Shouldly;

namespace Lopen.Core.Tests;

public class LoopConfigServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _userConfigPath;
    private readonly string _projectConfigPath;

    public LoopConfigServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _userConfigPath = Path.Combine(_testDir, "user", "loop-config.json");
        _projectConfigPath = Path.Combine(_testDir, "project", "loop-config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadConfigAsync_NoFiles_ReturnsDefaults()
    {
        var service = new LoopConfigService(_userConfigPath, _projectConfigPath);

        var config = await service.LoadConfigAsync();

        config.Model.ShouldBe("claude-opus-4.5");
        config.PlanPromptPath.ShouldBe("PLAN.PROMPT.md");
    }

    [Fact]
    public async Task LoadConfigAsync_WithUserConfig_LoadsUserConfig()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_userConfigPath)!);
        await File.WriteAllTextAsync(_userConfigPath, """
        {
            "model": "claude-opus-4.5",
            "planPromptPath": "custom-plan.md"
        }
        """);

        var service = new LoopConfigService(_userConfigPath, _projectConfigPath);
        var config = await service.LoadConfigAsync();

        config.Model.ShouldBe("claude-opus-4.5");
        config.PlanPromptPath.ShouldBe("custom-plan.md");
    }

    [Fact]
    public async Task LoadConfigAsync_ProjectOverridesUser()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_userConfigPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(_projectConfigPath)!);

        await File.WriteAllTextAsync(_userConfigPath, """
        {
            "model": "user-model",
            "planPromptPath": "user-plan.md"
        }
        """);

        await File.WriteAllTextAsync(_projectConfigPath, """
        {
            "model": "project-model"
        }
        """);

        var service = new LoopConfigService(_userConfigPath, _projectConfigPath);
        var config = await service.LoadConfigAsync();

        config.Model.ShouldBe("project-model"); // Project overrides
        config.PlanPromptPath.ShouldBe("user-plan.md"); // User config preserved
    }

    [Fact]
    public async Task LoadConfigAsync_CustomConfigOverridesAll()
    {
        var customPath = Path.Combine(_testDir, "custom-config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_userConfigPath)!);

        await File.WriteAllTextAsync(_userConfigPath, """{ "model": "user-model" }""");
        await File.WriteAllTextAsync(customPath, """{ "model": "custom-model" }""");

        var service = new LoopConfigService(_userConfigPath, _projectConfigPath);
        var config = await service.LoadConfigAsync(customPath);

        config.Model.ShouldBe("custom-model");
    }

    [Fact]
    public async Task SaveUserConfigAsync_CreatesDirectoryAndFile()
    {
        var service = new LoopConfigService(_userConfigPath, _projectConfigPath);
        var config = new LoopConfig { Model = "saved-model" };

        await service.SaveUserConfigAsync(config);

        File.Exists(_userConfigPath).ShouldBeTrue();
        var content = await File.ReadAllTextAsync(_userConfigPath);
        content.ShouldContain("saved-model");
    }

    [Fact]
    public async Task SaveProjectConfigAsync_CreatesDirectoryAndFile()
    {
        var service = new LoopConfigService(_userConfigPath, _projectConfigPath);
        var config = new LoopConfig { Model = "project-saved" };

        await service.SaveProjectConfigAsync(config);

        File.Exists(_projectConfigPath).ShouldBeTrue();
        var content = await File.ReadAllTextAsync(_projectConfigPath);
        content.ShouldContain("project-saved");
    }

    [Fact]
    public async Task ResetUserConfigAsync_DeletesFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_userConfigPath)!);
        await File.WriteAllTextAsync(_userConfigPath, "{}");

        var service = new LoopConfigService(_userConfigPath, _projectConfigPath);
        await service.ResetUserConfigAsync();

        File.Exists(_userConfigPath).ShouldBeFalse();
    }

    [Fact]
    public async Task LoadConfigAsync_InvalidJson_ReturnsDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_userConfigPath)!);
        await File.WriteAllTextAsync(_userConfigPath, "{ invalid json }");

        var service = new LoopConfigService(_userConfigPath, _projectConfigPath);
        var config = await service.LoadConfigAsync();

        config.Model.ShouldBe("claude-opus-4.5"); // Falls back to defaults
    }
}
