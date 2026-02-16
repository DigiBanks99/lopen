using System.CommandLine;
using Lopen.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Cli.Tests.Commands;

public class ConfigCommandTests
{
    private (CommandLineConfiguration config, StringWriter output, StringWriter error) CreateConfig(
        Dictionary<string, string?>? settings = null)
    {
        var configBuilder = new ConfigurationBuilder();
        if (settings is not null)
            configBuilder.AddInMemoryCollection(settings);

        var configRoot = configBuilder.Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfigurationRoot>(configRoot);
        var provider = services.BuildServiceProvider();

        var output = new StringWriter();
        var error = new StringWriter();

        var root = new RootCommand("test");
        root.Add(ConfigCommand.Create(provider, output, error));

        var config = new CommandLineConfiguration(root);
        return (config, output, error);
    }

    [Fact]
    public async Task Show_DisplaysConfigEntries()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Lopen:Models:Primary"] = "gpt-5",
            ["Lopen:Budget:MaxPremiumRequests"] = "100",
        };
        var (config, output, _) = CreateConfig(settings);

        var exitCode = await config.InvokeAsync(["config", "show"]);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("Lopen:Models:Primary", text);
        Assert.Contains("gpt-5", text);
    }

    [Fact]
    public async Task Show_NoEntries_DisplaysMessage()
    {
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["config", "show"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("No configuration entries found", output.ToString());
    }

    [Fact]
    public async Task Show_JsonFormat_ReturnsJson()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Lopen:Models:Primary"] = "gpt-5",
        };
        var (config, output, _) = CreateConfig(settings);

        var exitCode = await config.InvokeAsync(["config", "show", "--json"]);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("\"key\"", text);
        Assert.Contains("\"value\"", text);
        Assert.Contains("\"source\"", text);
    }

    [Fact]
    public async Task Show_DefaultFormat_IsTable()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Lopen:Budget:MaxPremiumRequests"] = "100",
        };
        var (config, output, _) = CreateConfig(settings);

        var exitCode = await config.InvokeAsync(["config", "show"]);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        // Table format has "Setting  Value  Source" header
        Assert.Contains("Setting", text);
        Assert.Contains("Value", text);
        Assert.Contains("Source", text);
    }
}
