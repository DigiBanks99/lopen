using System.CommandLine;
using Lopen.Commands;
using Lopen.Tui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Lopen.Cli.Tests.Commands;

/// <summary>
/// Tests for the 'lopen test tui' component gallery command.
/// Covers TUI-42: lopen test tui launches component gallery.
/// </summary>
public class TestCommandTests
{
    private static (CommandLineConfiguration config, StringWriter output) CreateConfig(
        bool includeGallery = true)
    {
        var builder = Host.CreateApplicationBuilder([]);

        if (includeGallery)
            builder.Services.AddLopenTui();

        var host = builder.Build();
        var output = new StringWriter();
        var rootCommand = new RootCommand("Lopen — test");
        rootCommand.Add(TestCommand.Create(host.Services, output));
        return (new CommandLineConfiguration(rootCommand), output);
    }

    [Fact]
    public async Task TestTui_Help_ReturnsSuccess()
    {
        var (config, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["test", "tui", "--help"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task TestTui_NoGallery_ReturnsFailure()
    {
        var (config, output) = CreateConfig(includeGallery: false);

        var exitCode = await config.InvokeAsync(["test", "tui", "--list"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("not available", output.ToString());
    }

    [Fact]
    public async Task TestTui_WithGallery_ListMode_ListsComponents()
    {
        var (config, output) = CreateConfig();

        var exitCode = await config.InvokeAsync(["test", "tui", "--list"]);

        Assert.Equal(0, exitCode);
        var outputText = output.ToString();
        Assert.Contains("Component Gallery", outputText);
        Assert.Contains("TopPanel", outputText);
    }

    [Fact]
    public async Task Test_Help_ReturnsSuccess()
    {
        var (config, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["test", "--help"]);

        Assert.Equal(0, exitCode);
    }

    // ==================== TUI-49: All components tested via lopen test tui ====================

    [Fact]
    public async Task TestTui_ListMode_ListsAllRegisteredComponents()
    {
        var (config, output) = CreateConfig();

        var exitCode = await config.InvokeAsync(["test", "tui", "--list"]);

        Assert.Equal(0, exitCode);
        var outputText = output.ToString();

        // Verify key components are listed
        Assert.Contains("TopPanel", outputText);
        Assert.Contains("ContextPanel", outputText);
        Assert.Contains("ActivityPanel", outputText);
        Assert.Contains("PromptArea", outputText);
        Assert.Contains("LandingPage", outputText);
    }

    [Fact]
    public void TestTui_AllGalleryComponents_ArePreviewableAndFunctional()
    {
        var builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenTui();
        var host = builder.Build();

        var gallery = host.Services.GetRequiredService<IComponentGallery>();
        var components = gallery.GetAll();
        Assert.True(components.Count >= 14);

        foreach (var component in components)
        {
            var previewable = Assert.IsAssignableFrom<IPreviewableComponent>(component);
            var lines = previewable.RenderPreview(80, 24);
            Assert.NotNull(lines);
            Assert.NotEmpty(lines);
        }
    }
}
