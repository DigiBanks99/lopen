using System.CommandLine;
using Lopen.Core;

var rootCommand = new RootCommand("Lopen - GitHub Copilot CLI");

// Version service uses the CLI assembly to get version
var versionService = new VersionService(typeof(Program).Assembly);

// Format option for structured output
var formatOption = new Option<string>("--format")
{
    Description = "Output format (text, json)",
    DefaultValueFactory = _ => "text"
};
formatOption.Aliases.Add("-f");
formatOption.AcceptOnlyFromAmong("text", "json");

// Version command
var versionCommand = new Command("version", "Display version information");
versionCommand.Options.Add(formatOption);
versionCommand.SetAction(parseResult =>
{
    var format = parseResult.GetValue(formatOption);
    if (format == "json")
    {
        Console.WriteLine(versionService.FormatAsJson());
    }
    else
    {
        Console.WriteLine(versionService.FormatAsText("lopen"));
    }
    return 0;
});
rootCommand.Subcommands.Add(versionCommand);

// Set action for root command (when no subcommand given)
rootCommand.SetAction(parseResult =>
{
    Console.WriteLine("Use --help for available commands");
    return 0;
});

return rootCommand.Parse(args).Invoke();
