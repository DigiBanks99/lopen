using System.CommandLine;
using Lopen.Auth;
using Lopen.Commands;
using Lopen.Configuration;
using Lopen.Core;
using Lopen.Llm;
using Lopen.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLopenConfiguration();
builder.Services.AddLopenAuth();
builder.Services.AddLopenCore();
builder.Services.AddLopenStorage();
builder.Services.AddLopenLlm();

using var host = builder.Build();

var rootCommand = new RootCommand("Lopen — AI-powered software engineering workflow");

rootCommand.SetAction((_) =>
{
    Console.WriteLine("Lopen CLI is ready.");
});

rootCommand.Add(AuthCommand.Create(host.Services));
rootCommand.Add(SessionCommand.Create(host.Services));
rootCommand.Add(ConfigCommand.Create(host.Services));
rootCommand.Add(RevertCommand.Create(host.Services));

var config = new CommandLineConfiguration(rootCommand);

return await config.InvokeAsync(args);
