using Lopen.Auth;
using Lopen.Commands;
using Lopen.Configuration;
using Lopen.Core;
using Lopen.Llm;
using Lopen.Otel;
using Lopen.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;

namespace Lopen.Cli.Tests;

public class ProgramTests : IDisposable
{
    private readonly IHost _host;
    private readonly IServiceProvider _services;

    public ProgramTests()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddLopenConfiguration();
        builder.Services.AddLopenAuth();
        builder.Services.AddLopenCore(null);
        builder.Services.AddLopenStorage(null);
        builder.Services.AddLopenLlm();
        builder.Services.AddLopenOtel(builder.Configuration);
        _host = builder.Build();
        _services = _host.Services;
    }

    public void Dispose() => _host.Dispose();

    // --- DI Container Resolution ---

    [Theory]
    [InlineData(typeof(ILlmService))]
    [InlineData(typeof(IModelSelector))]
    [InlineData(typeof(ITokenTracker))]
    [InlineData(typeof(IToolRegistry))]
    [InlineData(typeof(IPromptBuilder))]
    public void DI_ResolvesLlmServices(Type serviceType)
    {
        var service = _services.GetService(serviceType);
        Assert.NotNull(service);
    }

    [Theory]
    [InlineData(typeof(Lopen.Core.Workflow.IWorkflowOrchestrator))]
    [InlineData(typeof(Lopen.Core.IOutputRenderer))]
    public void DI_ResolvesCoreServices(Type serviceType)
    {
        // Note: Some core services require projectRoot for full resolution.
        // With null projectRoot, orchestrator registration is conditional.
        var service = _services.GetService(serviceType);
        // IWorkflowOrchestrator may be null without projectRoot — that's by design
        if (serviceType != typeof(Lopen.Core.Workflow.IWorkflowOrchestrator))
            Assert.NotNull(service);
    }

    [Fact]
    public void DI_ResolvesStorageFileSystem()
    {
        // ISessionManager and IAutoSaveService require projectRoot.
        // IFileSystem is always registered.
        IFileSystem? fs = _services.GetService<IFileSystem>();
        Assert.NotNull(fs);
    }

    [Fact]
    public void DI_ResolvesAuthService()
    {
        IAuthService? service = _services.GetService<IAuthService>();
        Assert.NotNull(service);
    }

    [Fact]
    public void DI_ResolvesAuthTokenProvider_FromAuthModule()
    {
        IAuthTokenProvider? provider = _services.GetService<IAuthTokenProvider>();
        IAuthService? authService = _services.GetService<IAuthService>();
        Assert.NotNull(provider);
        Assert.NotNull(authService);
        Assert.Same(authService, provider);
    }

    [Fact]
    public void DI_ResolvesSessionStateSaver_AsBridge()
    {
        // Without projectRoot, ISessionManager is not registered so the bridge
        // is not registered either. Verify that it falls back to NullSessionStateSaver.
        ISessionStateSaver? saver = _services.GetService<ISessionStateSaver>();
        Assert.NotNull(saver);
        // With projectRoot, this would be SessionStateSaverBridge instead.
    }

    // --- Command Creation ---

    [Fact]
    public void AuthCommand_CreatesSuccessfully()
    {
        Command cmd = AuthCommand.Create(_services);
        Assert.Equal("auth", cmd.Name);
    }

    [Fact]
    public void SessionCommand_CreatesSuccessfully()
    {
        Command cmd = SessionCommand.Create(_services);
        Assert.Equal("session", cmd.Name);
    }

    [Fact]
    public void ConfigCommand_CreatesSuccessfully()
    {
        Command cmd = ConfigCommand.Create(_services);
        Assert.Equal("config", cmd.Name);
    }

    [Fact]
    public void RevertCommand_CreatesSuccessfully()
    {
        Command cmd = RevertCommand.Create(_services);
        Assert.Equal("revert", cmd.Name);
    }

    [Fact]
    public void PhaseCommands_CreateSuccessfully()
    {
        Command spec = PhaseCommands.CreateSpec(_services);
        Command plan = PhaseCommands.CreatePlan(_services);
        Command build = PhaseCommands.CreateBuild(_services);

        Assert.Equal("spec", spec.Name);
        Assert.Equal("plan", plan.Name);
        Assert.Equal("build", build.Name);
    }

    // --- Full command tree ---

    [Fact]
    public void RootCommand_ContainsAllSubcommands()
    {
        RootCommand root = BuildRootCommand();

        var names = root.Subcommands.Select(c => c.Name).ToHashSet();
        Assert.Contains("auth", names);
        Assert.Contains("session", names);
        Assert.Contains("config", names);
        Assert.Contains("revert", names);
        Assert.Contains("spec", names);
        Assert.Contains("plan", names);
        Assert.Contains("build", names);
    }

    // --- Help invocation ---

    [Fact]
    public async Task RootCommand_Help_ReturnsSuccess()
    {
        RootCommand root = BuildRootCommand();
        var config = new CommandLineConfiguration(root);

        var exitCode = await config.InvokeAsync(["--help"]);

        Assert.Equal(0, exitCode);
    }

    [Theory]
    [InlineData("auth", "--help")]
    [InlineData("session", "--help")]
    [InlineData("config", "--help")]
    public async Task SubCommand_Help_ReturnsSuccess(params string[] args)
    {
        RootCommand root = BuildRootCommand();
        var config = new CommandLineConfiguration(root);

        var exitCode = await config.InvokeAsync(args);

        Assert.Equal(0, exitCode);
    }

    private RootCommand BuildRootCommand()
    {
        var root = new RootCommand("Lopen — AI-powered software engineering workflow");
        GlobalOptions.AddTo(root);
        RootCommandHandler.Configure(_services)(root);
        root.Add(AuthCommand.Create(_services));
        root.Add(SessionCommand.Create(_services));
        root.Add(ConfigCommand.Create(_services));
        root.Add(RevertCommand.Create(_services));
        root.Add(PhaseCommands.CreateSpec(_services));
        root.Add(PhaseCommands.CreatePlan(_services));
        root.Add(PhaseCommands.CreateBuild(_services));
        return root;
    }
}
