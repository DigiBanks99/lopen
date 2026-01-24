using System.CommandLine;
using Lopen.Core;

var rootCommand = new RootCommand("Lopen - GitHub Copilot CLI");

// Services
var versionService = new VersionService(typeof(Program).Assembly);
var helpService = new HelpService();

// Format option for structured output (reusable)
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

// Help command - format option needs separate instance to avoid conflicts
var helpFormatOption = new Option<string>("--format")
{
    Description = "Output format (text, json)",
    DefaultValueFactory = _ => "text"
};
helpFormatOption.Aliases.Add("-f");
helpFormatOption.AcceptOnlyFromAmong("text", "json");

var commandArg = new Argument<string?>("command")
{
    Description = "Command to get help for",
    DefaultValueFactory = _ => null
};

var helpCommand = new Command("help", "Display help information");
helpCommand.Arguments.Add(commandArg);
helpCommand.Options.Add(helpFormatOption);
helpCommand.SetAction(parseResult =>
{
    var format = parseResult.GetValue(helpFormatOption);
    var commandName = parseResult.GetValue(commandArg);

    // Build command info from root command
    var commands = rootCommand.Subcommands
        .Where(c => c.Name != "help") // Exclude help from list
        .Select(c => new CommandInfo(c.Name, c.Description ?? ""))
        .ToList();

    if (commandName is not null)
    {
        // Find specific command
        var cmd = rootCommand.Subcommands.FirstOrDefault(c => c.Name == commandName);
        if (cmd is null)
        {
            Console.Error.WriteLine($"Unknown command: {commandName}");
            return 1;
        }

        var subcommands = cmd.Subcommands
            .Select(s => new CommandInfo(s.Name, s.Description ?? ""))
            .ToList();
        var cmdInfo = new CommandInfo(cmd.Name, cmd.Description ?? "", subcommands.Count > 0 ? subcommands : null);

        if (format == "json")
        {
            Console.WriteLine(helpService.FormatCommandHelpAsJson(cmdInfo));
        }
        else
        {
            Console.WriteLine(helpService.FormatCommandHelpAsText(cmdInfo));
        }
    }
    else
    {
        // List all commands
        if (format == "json")
        {
            Console.WriteLine(helpService.FormatCommandListAsJson("lopen", "GitHub Copilot CLI", commands));
        }
        else
        {
            Console.WriteLine(helpService.FormatCommandListAsText("lopen", "GitHub Copilot CLI", commands));
        }
    }
    return 0;
});
rootCommand.Subcommands.Add(helpCommand);

// Set action for root command (when no subcommand given)
rootCommand.SetAction(parseResult =>
{
    Console.WriteLine("Use --help for available commands");
    return 0;
});

return rootCommand.Parse(args).Invoke();
