using System.CommandLine;
using Lopen.Auth;
using Lopen.Commands;
using Lopen.Configuration;
using Lopen.Core;
using Lopen.Llm;
using Lopen.Otel;
using Lopen.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

var projectRoot = Lopen.ProjectRootDiscovery.FindProjectRoot(Directory.GetCurrentDirectory());

builder.Services.AddLopenConfiguration();
builder.Services.AddLopenAuth();
builder.Services.AddSingleton<IGitHubTokenProvider, Lopen.AuthBridgeTokenProvider>();
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
