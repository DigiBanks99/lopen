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

// Chat command
var chatCommand = new Command("chat", "Start AI chat session");

var modelOption = new Option<string>("--model")
{
    Description = "AI model to use",
    DefaultValueFactory = _ => "gpt-5"
};
modelOption.Aliases.Add("-m");

var streamingOption = new Option<bool>("--streaming")
{
    Description = "Enable streaming output (default: true)",
    DefaultValueFactory = _ => true
};
streamingOption.Aliases.Add("-s");

var resumeOption = new Option<string?>("--resume")
{
    Description = "Resume a previous session by ID"
};
resumeOption.Aliases.Add("-r");

var promptArg = new Argument<string?>("prompt")
{
    Description = "Single query (omit for interactive mode)",
    DefaultValueFactory = _ => null
};

chatCommand.Options.Add(modelOption);
chatCommand.Options.Add(streamingOption);
chatCommand.Options.Add(resumeOption);
chatCommand.Arguments.Add(promptArg);

chatCommand.SetAction(async parseResult =>
{
    var model = parseResult.GetValue(modelOption);
    var streaming = parseResult.GetValue(streamingOption);
    var resumeId = parseResult.GetValue(resumeOption);
    var prompt = parseResult.GetValue(promptArg);

    // Create Copilot service
    await using var copilotService = new CopilotService();

    // Check authentication
    try
    {
        var authStatus = await copilotService.GetAuthStatusAsync();
        if (!authStatus.IsAuthenticated)
        {
            output.Error("Not authenticated. Run 'copilot auth login' first.");
            return ExitCodes.AuthenticationError;
        }
    }
    catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
    {
        output.Error($"Copilot CLI not available: {ex.Message}");
        output.Muted("Install Copilot CLI: https://docs.github.com/en/copilot");
        return ExitCodes.CopilotError;
    }

    // Create or resume session
    ICopilotSession session;
    if (!string.IsNullOrEmpty(resumeId))
    {
        try
        {
            session = await copilotService.ResumeSessionAsync(resumeId);
            output.Info($"Resumed session: {resumeId}");
        }
        catch (InvalidOperationException ex)
        {
            output.Error($"Failed to resume session: {ex.Message}");
            return ExitCodes.GeneralError;
        }
    }
    else
    {
        session = await copilotService.CreateSessionAsync(new CopilotSessionOptions
        {
            Model = model!,
            Streaming = streaming
        });
    }
    await using var _ = session;

    // Single query mode
    if (!string.IsNullOrEmpty(prompt))
    {
        await foreach (var chunk in session.StreamAsync(prompt))
        {
            Console.Write(chunk);
        }
        Console.WriteLine();
        output.Muted($"Session ID: {session.SessionId}");
        return ExitCodes.Success;
    }

    // Interactive mode
    output.Info($"Chat session started (model: {model}). Type 'exit' or 'quit' to end.");
    output.Muted($"Session ID: {session.SessionId}");

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    while (!cts.IsCancellationRequested)
    {
        output.Write("You: ");
        var input = Console.ReadLine();

        if (input is null or "exit" or "quit")
            break;

        if (string.IsNullOrWhiteSpace(input))
            continue;

        output.Write("AI: ");
        try
        {
            await foreach (var chunk in session.StreamAsync(input, cts.Token))
            {
                Console.Write(chunk);
            }
            Console.WriteLine();
        }
        catch (OperationCanceledException)
        {
            await session.AbortAsync();
            Console.WriteLine("\n[Aborted]");
            break;
        }
    }

    output.Info("Goodbye!");
    return ExitCodes.Success;
});
rootCommand.Subcommands.Add(chatCommand);

// Sessions command with subcommands
var sessionsCommand = new Command("sessions", "Manage chat sessions");

// sessions list
var sessionsListCommand = new Command("list", "List available sessions");
sessionsListCommand.SetAction(async parseResult =>
{
    await using var copilotService = new CopilotService();

    try
    {
        var sessions = await copilotService.ListSessionsAsync();

        if (sessions.Count == 0)
        {
            output.Info("No sessions found.");
            return ExitCodes.Success;
        }

        output.Info($"Found {sessions.Count} session(s):");
        foreach (var session in sessions)
        {
            var summary = session.Summary ?? "(no summary)";
            output.WriteLine($"  {session.SessionId}");
            output.Muted($"    Modified: {session.ModifiedTime:g}");
            output.Muted($"    Summary: {summary}");
        }
        return ExitCodes.Success;
    }
    catch (Exception ex)
    {
        output.Error($"Failed to list sessions: {ex.Message}");
        return ExitCodes.CopilotError;
    }
});
sessionsCommand.Subcommands.Add(sessionsListCommand);

// sessions delete
var sessionIdArg = new Argument<string>("session-id")
{
    Description = "Session ID to delete"
};
var sessionsDeleteCommand = new Command("delete", "Delete a session");
sessionsDeleteCommand.Arguments.Add(sessionIdArg);
sessionsDeleteCommand.SetAction(async parseResult =>
{
    var sessionId = parseResult.GetValue(sessionIdArg);
    await using var copilotService = new CopilotService();

    try
    {
        await copilotService.DeleteSessionAsync(sessionId!);
        output.Success($"Session deleted: {sessionId}");
        return ExitCodes.Success;
    }
    catch (Exception ex)
    {
        output.Error($"Failed to delete session: {ex.Message}");
        return ExitCodes.CopilotError;
    }
});
sessionsCommand.Subcommands.Add(sessionsDeleteCommand);

rootCommand.Subcommands.Add(sessionsCommand);

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
    autoCompleter.RegisterCommand("chat", "Start AI chat session",
        options: ["--model", "-m", "--streaming", "-s", "--resume", "-r"]);
    autoCompleter.RegisterCommand("sessions", "Manage chat sessions",
        subcommands: ["list", "delete"]);
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
