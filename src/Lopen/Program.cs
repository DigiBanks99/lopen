using System.CommandLine;
using Lopen.Auth;
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

var config = new CommandLineConfiguration(rootCommand);

return await config.InvokeAsync(args);
