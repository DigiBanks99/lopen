using System.CommandLine;
using Lopen.Core;

var rootCommand = new RootCommand("Lopen - GitHub Copilot CLI");

// Services
var versionService = new VersionService(typeof(Program).Assembly);
var helpService = new HelpService();
var credentialStore = new FileCredentialStore();
var authService = new AuthService(credentialStore);
var output = new ConsoleOutput();

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

// Auth command with subcommands
var authCommand = new Command("auth", "Authentication commands");

// auth login
var loginCommand = new Command("login", "Login to GitHub");
loginCommand.SetAction(async parseResult =>
{
    var status = await authService.GetStatusAsync();
    if (status.IsAuthenticated)
    {
        Console.WriteLine($"Already authenticated via {status.Source}");
        return 0;
    }

    Console.WriteLine("To authenticate, set the GITHUB_TOKEN environment variable");
    Console.WriteLine("or run: lopen auth login --token <your-token>");
    Console.WriteLine();
    Console.WriteLine("Get a token from: https://github.com/settings/tokens");
    Console.WriteLine("Required scopes: copilot, read:user");
    return 0;
});

var tokenOption = new Option<string?>("--token")
{
    Description = "GitHub personal access token"
};
loginCommand.Options.Add(tokenOption);
loginCommand.SetAction(async parseResult =>
{
    var token = parseResult.GetValue(tokenOption);
    if (!string.IsNullOrEmpty(token))
    {
        await authService.StoreTokenAsync(token);
        output.Success("Token stored successfully.");
        return 0;
    }

    var status = await authService.GetStatusAsync();
    if (status.IsAuthenticated)
    {
        output.Info($"Already authenticated via {status.Source}");
        return 0;
    }

    output.Info("To authenticate, provide a token:");
    output.WriteLine("  lopen auth login --token <your-token>");
    output.WriteLine();
    output.Muted("Or set the GITHUB_TOKEN environment variable.");
    output.Muted("Get a token from: https://github.com/settings/tokens");
    output.Muted("Required scopes: copilot, read:user");
    return 0;
});
authCommand.Subcommands.Add(loginCommand);

// auth status
var statusCommand = new Command("status", "Check authentication status");
statusCommand.SetAction(async parseResult =>
{
    var status = await authService.GetStatusAsync();
    if (status.IsAuthenticated)
    {
        output.Success("Authenticated");
        output.KeyValue("Source", status.Source ?? "unknown");
    }
    else
    {
        output.Warning("Not authenticated");
        output.Muted("Run 'lopen auth login' to authenticate.");
    }
    return 0;
});
authCommand.Subcommands.Add(statusCommand);

// auth logout
var logoutCommand = new Command("logout", "Clear stored credentials");
logoutCommand.SetAction(async parseResult =>
{
    await authService.ClearAsync();
    output.Success("Credentials cleared.");
    return 0;
});
authCommand.Subcommands.Add(logoutCommand);

rootCommand.Subcommands.Add(authCommand);

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

// REPL command
var replCommand = new Command("repl", "Start interactive REPL mode");
replCommand.SetAction(async parseResult =>
{
    // Set up auto-completer with available commands
    var autoCompleter = new CommandAutoCompleter();
    autoCompleter.RegisterCommand("version", "Display version information", options: ["--format", "-f"]);
    autoCompleter.RegisterCommand("help", "Display help information", options: ["--format", "-f"]);
    autoCompleter.RegisterCommand("auth", "Authentication commands", 
        subcommands: ["login", "logout", "status"], 
        options: ["--token"]);
    autoCompleter.RegisterCommand("repl", "Start interactive REPL mode");
    autoCompleter.RegisterCommand("exit", "Exit the REPL");
    autoCompleter.RegisterCommand("quit", "Exit the REPL");
    
    // Set up command history with persistence
    var history = new PersistentCommandHistory();
    var consoleInput = new ConsoleInputWithHistory(history, autoCompleter);
    
    // Set up session state
    var sessionStateService = new SessionStateService(authService);
    
    var replService = new ReplService(consoleInput, output, sessionStateService);
    
    return await replService.RunAsync(async cmdArgs =>
    {
        // Execute command using the root command parser
        var result = rootCommand.Parse(cmdArgs);
        return await result.InvokeAsync();
    });
});
rootCommand.Subcommands.Add(replCommand);

// Set action for root command (when no subcommand given)
rootCommand.SetAction(parseResult =>
{
    Console.WriteLine("Use --help for available commands or 'lopen repl' for interactive mode");
    return 0;
});

return rootCommand.Parse(args).Invoke();
