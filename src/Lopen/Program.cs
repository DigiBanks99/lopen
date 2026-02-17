using System.CommandLine;
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

var builder = Host.CreateApplicationBuilder(args);

var projectRoot = Lopen.ProjectRootDiscovery.FindProjectRoot(Directory.GetCurrentDirectory());

builder.Services.AddLopenConfiguration();
builder.Services.AddLopenAuth();
builder.Services.AddLopenCore(projectRoot);
builder.Services.AddLopenStorage(projectRoot);
builder.Services.AddLopenLlm();
builder.Services.AddLopenTui();
builder.Services.UseRealTui();
builder.Services.AddTopPanelDataProvider();
builder.Services.AddContextPanelDataProvider();
builder.Services.AddActivityPanelDataProvider();
builder.Services.AddUserPromptQueue();
builder.Services.AddSessionDetector();
builder.Services.AddTuiOutputRenderer();
builder.Services.AddLopenOtel(builder.Configuration);

using var host = builder.Build();

var rootCommand = new RootCommand("Lopen — AI-powered software engineering workflow");
GlobalOptions.AddTo(rootCommand);

RootCommandHandler.Configure(host.Services)(rootCommand);

rootCommand.Add(AuthCommand.Create(host.Services));
rootCommand.Add(SessionCommand.Create(host.Services));
rootCommand.Add(ConfigCommand.Create(host.Services));
rootCommand.Add(RevertCommand.Create(host.Services));
rootCommand.Add(PhaseCommands.CreateSpec(host.Services));
rootCommand.Add(PhaseCommands.CreatePlan(host.Services));
rootCommand.Add(PhaseCommands.CreateBuild(host.Services));
rootCommand.Add(TestCommand.Create(host.Services));

var config = new CommandLineConfiguration(rootCommand);

return await config.InvokeAsync(args);
