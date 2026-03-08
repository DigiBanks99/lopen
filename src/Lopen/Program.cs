using Lopen.Auth;
using Lopen.Commands;
using Lopen.Configuration;
using Lopen.Core;
using Lopen.Llm;
using Lopen.Otel;
using Lopen.Storage;
using Lopen.Tui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

var projectRoot = Lopen.ProjectRootDiscovery.FindProjectRoot(Directory.GetCurrentDirectory());

// Check if running in headless mode before registering services
bool isHeadless = args.Contains("--headless") || args.Contains("-q");

builder.Services.AddLopenConfiguration();
builder.Services.AddLopenAuth();
builder.Services.AddSingleton<IGitHubTokenProvider, Lopen.AuthBridgeTokenProvider>();

// Register TUI before Core so TuiOutputRenderer takes precedence over HeadlessRenderer
// via TryAddSingleton<IOutputRenderer> in Core.
if (!isHeadless)
{
    builder.Services.AddLopenTui();
}

builder.Services.AddLopenCore(projectRoot);
builder.Services.AddLopenStorage(projectRoot);
if (projectRoot is not null)
{
    builder.Services.AddSingleton<ISessionStateSaver, Lopen.SessionStateSaverBridge>();
}
builder.Services.AddLopenLlm();
builder.Services.AddLopenOtel(builder.Configuration);

using IHost host = builder.Build();

RootCommand rootCommand = new("Lopen — AI-powered software engineering workflow");
GlobalOptions.AddTo(rootCommand);

RootCommandHandler.Configure(host.Services)(rootCommand);

rootCommand.Add(AuthCommand.Create(host.Services));
rootCommand.Add(SessionCommand.Create(host.Services));
rootCommand.Add(ConfigCommand.Create(host.Services));
rootCommand.Add(RevertCommand.Create(host.Services));
rootCommand.Add(PhaseCommands.CreateSpec(host.Services));
rootCommand.Add(PhaseCommands.CreatePlan(host.Services));
rootCommand.Add(PhaseCommands.CreateBuild(host.Services));

CommandLineConfiguration config = new(rootCommand);

return await config.InvokeAsync(args);
