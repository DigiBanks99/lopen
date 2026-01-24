using System.CommandLine;
using Lopen.Core;

var rootCommand = new RootCommand("Lopen - GitHub Copilot CLI");

// Version service uses the CLI assembly to get version
var versionService = new VersionService(typeof(Program).Assembly);

// Set action for root command (when no subcommand given)
rootCommand.SetAction(parseResult =>
{
    Console.WriteLine("Use --help for available commands");
    return 0;
});

return rootCommand.Parse(args).Invoke();
