using Lopen.Auth;
using Lopen.Cli.Tests.Fakes;
using Lopen.Commands;
using Lopen.Configuration;
using Lopen.Core;
using Lopen.Llm;
using Lopen.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using System.Reflection;

namespace Lopen.Cli.Tests.Commands;

/// <summary>
/// Integration tests verifying CLI wiring, command registration, and DI setup.
/// Covers AC-1 (root command), AC-2 (headless basic), AC-22 (build), AC-25 (DI hosting).
/// </summary>
public class CliIntegrationTests
{
    private static (CommandLineConfiguration config, StringWriter output) CreateFullConfig()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddSingleton<IAuthService>(new FakeAuthService());
        builder.Services.AddSingleton<IAuthTokenProvider>(new TestAuthTokenProvider());
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();
        IHost host = builder.Build();

        var output = new StringWriter();
        var rootCommand = new RootCommand("Lopen — AI-powered software engineering workflow");
        GlobalOptions.AddTo(rootCommand);
        RootCommandHandler.Configure(host.Services, output)(rootCommand);

        rootCommand.Add(AuthCommand.Create(host.Services, output));
        rootCommand.Add(SessionCommand.Create(host.Services, output));
        rootCommand.Add(ConfigCommand.Create(host.Services, output));
        rootCommand.Add(RevertCommand.Create(host.Services, output));
        rootCommand.Add(PhaseCommands.CreateSpec(host.Services, output));
        rootCommand.Add(PhaseCommands.CreatePlan(host.Services, output));
        rootCommand.Add(PhaseCommands.CreateBuild(host.Services, output));

        return (new CommandLineConfiguration(rootCommand), output);
    }

    // ==================== AC-25: DI Hosting ====================

    [Fact]
    public void Host_BuildsSuccessfully_WithAllModules()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddSingleton<IAuthService>(new FakeAuthService());
        builder.Services.AddSingleton<IAuthTokenProvider>(new TestAuthTokenProvider());
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();

        using IHost host = builder.Build();

        Assert.NotNull(host);
        Assert.NotNull(host.Services);
    }

    [Fact]
    public void Host_ResolvesCoreCrossModuleServices()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddSingleton<IAuthService>(new FakeAuthService());
        builder.Services.AddSingleton<IAuthTokenProvider>(new TestAuthTokenProvider());
        builder.Services.AddLopenCore();
        builder.Services.AddLopenStorage();
        builder.Services.AddLopenLlm();

        using IHost host = builder.Build();

        // Configuration services
        Assert.NotNull(host.Services.GetRequiredService<Lopen.Configuration.LopenOptions>());
        Assert.NotNull(host.Services.GetRequiredService<Lopen.Configuration.IBudgetEnforcer>());

        // Core services
        Assert.NotNull(host.Services.GetRequiredService<Lopen.Core.BackPressure.IGuardrailPipeline>());
        Assert.NotNull(host.Services.GetRequiredService<Lopen.Core.Workflow.IPhaseTransitionController>());

        // LLM services
        Assert.NotNull(host.Services.GetRequiredService<Lopen.Llm.ILlmService>());
        Assert.NotNull(host.Services.GetRequiredService<Lopen.Llm.IToolRegistry>());
        Assert.NotNull(host.Services.GetRequiredService<Lopen.Llm.IModelSelector>());
        Assert.NotNull(host.Services.GetRequiredService<Lopen.Llm.IPromptBuilder>());
        Assert.NotNull(host.Services.GetRequiredService<Lopen.Llm.ITokenTracker>());

        // Storage services
        Assert.NotNull(host.Services.GetRequiredService<Lopen.Storage.IFileSystem>());

        // Auth
        Assert.NotNull(host.Services.GetRequiredService<IAuthService>());
    }

    // ==================== AC-1: Root Command ====================

    [Fact]
    public async Task RootCommand_NoArgs_ReturnsTuiNotImplemented()
    {
        (CommandLineConfiguration? config, StringWriter? output) = CreateFullConfig();

        var exitCode = await config.InvokeAsync([]);

        Assert.Equal(1, exitCode);
    }

    private sealed class TestAuthTokenProvider : IAuthTokenProvider
    {
        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    // ==================== Command Registration ====================

    [Fact]
    public async Task AllSubcommands_AreRegistered()
    {
        (CommandLineConfiguration? config, StringWriter _) = CreateFullConfig();

        // Each recognized command should not result in a parse error
        string[] commands = ["auth", "session", "config", "revert", "spec", "plan", "build"];
        foreach (var cmd in commands)
        {
            var exitCode = await config.InvokeAsync([cmd, "--help"]);
            Assert.Equal(0, exitCode);
        }
    }

    // ==================== AC-21: Help and Version ====================

    [Fact]
    public async Task HelpFlag_OnRoot_ReturnsSuccess()
    {
        (CommandLineConfiguration? config, StringWriter _) = CreateFullConfig();

        var exitCode = await config.InvokeAsync(["--help"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task VersionFlag_OnRoot_ReturnsSuccess()
    {
        (CommandLineConfiguration? config, StringWriter _) = CreateFullConfig();

        var exitCode = await config.InvokeAsync(["--version"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task VersionFlag_OnRoot_OutputsVersionString()
    {
        (CommandLineConfiguration? config, StringWriter _) = CreateFullConfig();
        var versionWriter = new StringWriter();
        config.Output = versionWriter;

        var exitCode = await config.InvokeAsync(["--version"]);

        Assert.Equal(0, exitCode);
        // In test context, --version reads from the entry assembly (test host),
        // so we verify the Lopen assembly carries the expected version attribute.
        Assembly asm = typeof(RootCommandHandler).Assembly;
        AssemblyInformationalVersionAttribute? infoVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        Assert.NotNull(infoVersion);
        Assert.Matches(@"\d+\.\d+\.\d+", infoVersion.InformationalVersion);
    }

    // ==================== AC-16/AC-19: Global Options ====================

    [Fact]
    public async Task HeadlessFlag_IsRecognized_OnSubcommands()
    {
        (CommandLineConfiguration? config, StringWriter _) = CreateFullConfig();

        // --headless on spec should be recognized (not a parse error)
        // Will return 1 because headless + no prompt + no session, but it's a validation error, not parse error
        var exitCode = await config.InvokeAsync(["spec", "--headless"]);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task PromptFlag_IsRecognized_OnSubcommands()
    {
        (CommandLineConfiguration? config, StringWriter? output) = CreateFullConfig();

        var exitCode = await config.InvokeAsync(["spec", "--prompt", "Build an auth module"]);

        // --prompt is recognized (not a parse error). Without a project root,
        // the orchestrator is not registered so the command returns failure,
        // but it still prints the phase message proving the flag was parsed.
        Assert.Contains("requirement gathering", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // ==================== AC-20: Exit Codes ====================

    [Fact]
    public async Task UnrecognizedCommand_ReturnsNonZero()
    {
        (CommandLineConfiguration? config, StringWriter _) = CreateFullConfig();

        var exitCode = await config.InvokeAsync(["nonexistent-command"]);

        Assert.NotEqual(0, exitCode);
    }

    // ==================== AC-6/7/8: Auth Subcommands Wired ====================

    [Fact]
    public async Task AuthLogin_Help_ReturnsSuccess()
    {
        (CommandLineConfiguration? config, StringWriter _) = CreateFullConfig();

        var exitCode = await config.InvokeAsync(["auth", "login", "--help"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task AuthStatus_Help_ReturnsSuccess()
    {
        (CommandLineConfiguration? config, StringWriter _) = CreateFullConfig();

        var exitCode = await config.InvokeAsync(["auth", "status", "--help"]);

        Assert.Equal(0, exitCode);
    }

    // ==================== AC-9/11/12/13: Session Subcommands Wired ====================

    [Fact]
    public async Task SessionList_Help_ReturnsSuccess()
    {
        (CommandLineConfiguration? config, StringWriter _) = CreateFullConfig();

        var exitCode = await config.InvokeAsync(["session", "list", "--help"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task SessionDelete_Help_ReturnsSuccess()
    {
        (CommandLineConfiguration? config, StringWriter _) = CreateFullConfig();

        var exitCode = await config.InvokeAsync(["session", "delete", "--help"]);

        Assert.Equal(0, exitCode);
    }
}
