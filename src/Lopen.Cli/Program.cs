using System.CommandLine;
using Lopen.Core;

var rootCommand = new RootCommand("Lopen - GitHub Copilot CLI");

// Services
var versionService = new VersionService(typeof(Program).Assembly);
var helpService = new HelpService();

// Use secure credential storage with fallback to file-based storage
ICredentialStore credentialStore;
ITokenInfoStore tokenInfoStore;
var usingSecureStorage = false;

if (SecureCredentialStore.IsAvailable())
{
    var secureStore = new SecureCredentialStore();
    credentialStore = secureStore;
    tokenInfoStore = secureStore;
    usingSecureStorage = true;
    // Migrate credentials from file storage if present
    var fileStore = new FileCredentialStore();
    await CredentialMigration.MigrateIfNeededAsync(credentialStore, fileStore);
}
else
{
    var fileStore = new FileCredentialStore();
    credentialStore = fileStore;
    tokenInfoStore = fileStore;
}

var deviceFlowAuth = new DeviceFlowAuth();
var authService = new AuthService(credentialStore, tokenInfoStore, deviceFlowAuth);
var sessionStore = new FileSessionStore();
var output = new ConsoleOutput();
var welcomeHeaderRenderer = new SpectreWelcomeHeaderRenderer();
var errorRenderer = new SpectreErrorRenderer();
var progressRenderer = new SpectreProgressRenderer();

// Helper to show welcome header
void ShowWelcomeHeader()
{
    var context = new WelcomeHeaderContext
    {
        Version = versionService.GetVersion(),
        SessionName = "",
        ContextWindow = new ContextWindowInfo(),
        Preferences = new WelcomeHeaderPreferences { ShowLogo = true, ShowTip = true }
    };
    welcomeHeaderRenderer.RenderWelcomeHeader(context);
}

// Helper to show security warning when not using secure storage
void ShowSecureStorageWarningIfNeeded()
{
    if (!usingSecureStorage)
    {
        output.Warning("Secure credential storage not available.");
        output.Muted("Credentials stored with basic encryption in ~/.lopen/credentials.json");
        output.WriteLine();
        if (OperatingSystem.IsLinux())
        {
            output.Muted("To configure secure storage, set GCM_CREDENTIAL_STORE:");
            output.Muted("  export GCM_CREDENTIAL_STORE=cache         # in-memory (temporary)");
            output.Muted("  export GCM_CREDENTIAL_STORE=secretservice # GUI required");
            output.Muted("  export GCM_CREDENTIAL_STORE=gpg           # requires GPG/pass");
            output.Muted("See: https://aka.ms/gcm/credstores");
            output.WriteLine();
        }
    }
}

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

var tokenOption = new Option<string?>("--token")
{
    Description = "GitHub personal access token"
};
loginCommand.Options.Add(tokenOption);

loginCommand.SetAction(async parseResult =>
{
    var token = parseResult.GetValue(tokenOption);
    
    // If token provided, store it directly
    if (!string.IsNullOrEmpty(token))
    {
        ShowSecureStorageWarningIfNeeded();
        await authService.StoreTokenAsync(token);
        output.Success("Token stored successfully.");
        return ExitCodes.Success;
    }

    // Check if already authenticated
    var status = await authService.GetStatusAsync();
    if (status.IsAuthenticated)
    {
        output.Info($"Already authenticated via {status.Source}");
        return ExitCodes.Success;
    }

    // Try device flow authentication
    var config = deviceFlowAuth.GetConfig();
    if (config is not null)
    {
        output.Info("Starting GitHub device authorization...");
        
        var deviceCode = await deviceFlowAuth.StartDeviceFlowAsync();
        if (deviceCode is null)
        {
            output.Error("Failed to start device authorization.");
            output.Muted("Fallback: Use --token option with a personal access token.");
            return ExitCodes.AuthenticationError;
        }

        // Display user code and URL
        output.WriteLine();
        output.Info("Please visit the following URL in your browser:");
        output.WriteLine($"  {deviceCode.VerificationUri}");
        output.WriteLine();
        output.Info("Enter this code:");
        output.WriteLine($"  {deviceCode.UserCode}");
        output.WriteLine();
        output.Muted("Waiting for authorization... (press Ctrl+C to cancel)");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => 
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            var result = await deviceFlowAuth.PollForTokenAsync(deviceCode, cancellationToken: cts.Token);
            
            if (result.Success && result.AccessToken is not null)
            {
                output.WriteLine();
                ShowSecureStorageWarningIfNeeded();
                await authService.StoreTokenAsync(result.AccessToken);
                output.Success("Successfully authenticated!");
                return ExitCodes.Success;
            }
            else
            {
                output.WriteLine();
                output.Error(result.Error ?? "Authentication failed.");
                return ExitCodes.AuthenticationError;
            }
        }
        catch (OperationCanceledException)
        {
            output.WriteLine();
            output.Warning("Authentication cancelled.");
            return ExitCodes.Cancelled;
        }
    }

    // No OAuth config, fall back to token instructions
    output.Info("To authenticate, provide a token:");
    output.WriteLine("  lopen auth login --token <your-token>");
    output.WriteLine();
    output.Muted("Or set the GITHUB_TOKEN environment variable.");
    output.Muted("Get a token from: https://github.com/settings/tokens");
    output.Muted("Required scopes: copilot, read:user");
    return ExitCodes.Success;
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

var noHeaderChatOption = new Option<bool>("--no-header")
{
    Description = "Suppress welcome header",
    DefaultValueFactory = _ => false
};

var promptArg = new Argument<string?>("prompt")
{
    Description = "Single query (omit for interactive mode)",
    DefaultValueFactory = _ => null
};

chatCommand.Options.Add(modelOption);
chatCommand.Options.Add(streamingOption);
chatCommand.Options.Add(resumeOption);
chatCommand.Options.Add(noHeaderChatOption);
chatCommand.Arguments.Add(promptArg);

chatCommand.SetAction(async parseResult =>
{
    var model = parseResult.GetValue(modelOption);
    var streaming = parseResult.GetValue(streamingOption);
    var resumeId = parseResult.GetValue(resumeOption);
    var noHeader = parseResult.GetValue(noHeaderChatOption);
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
    if (!noHeader)
    {
        ShowWelcomeHeader();
    }
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

        var tableConfig = new TableConfig<CopilotSessionInfo>
        {
            Columns = new List<TableColumn<CopilotSessionInfo>>
            {
                new() { Header = "ID", Selector = s => s.SessionId },
                new() { Header = "Modified", Selector = s => s.ModifiedTime.ToString("g") },
                new() { Header = "Summary", Selector = s => s.Summary ?? "(no summary)" }
            },
            ShowRowCount = true,
            RowCountFormat = "{0} session(s)"
        };

        output.Table(sessions, tableConfig);
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

// REPL session commands
var replSessionCommand = new Command("repl-session", "Manage REPL session state");

// repl-session save
var saveNameArg = new Argument<string?>("name")
{
    Description = "Session name (optional, defaults to session ID)",
    Arity = ArgumentArity.ZeroOrOne
};
var replSessionSaveCommand = new Command("save", "Save current REPL session");
replSessionSaveCommand.Arguments.Add(saveNameArg);
replSessionSaveCommand.SetAction(async parseResult =>
{
    var name = parseResult.GetValue(saveNameArg);
    var stateService = new SessionStateService(authService, sessionStore);
    await stateService.InitializeAsync();

    try
    {
        await stateService.SaveSessionAsync(name);
        output.Success($"Session saved{(name is not null ? $": {name}" : "")}");
        return ExitCodes.Success;
    }
    catch (Exception ex)
    {
        output.Error($"Failed to save session: {ex.Message}");
        return ExitCodes.ConfigurationError;
    }
});
replSessionCommand.Subcommands.Add(replSessionSaveCommand);

// repl-session load
var loadSessionArg = new Argument<string>("session")
{
    Description = "Session ID or name to load"
};
var replSessionLoadCommand = new Command("load", "Load a saved REPL session");
replSessionLoadCommand.Arguments.Add(loadSessionArg);
replSessionLoadCommand.SetAction(async parseResult =>
{
    var sessionIdOrName = parseResult.GetValue(loadSessionArg);
    var stateService = new SessionStateService(authService, sessionStore);
    await stateService.InitializeAsync();

    try
    {
        var loaded = await stateService.LoadSessionAsync(sessionIdOrName!);
        if (loaded)
        {
            output.Success($"Session loaded: {sessionIdOrName}");
            output.KeyValue("Session ID", stateService.CurrentState.SessionId);
            output.KeyValue("Started", stateService.CurrentState.StartedAt.LocalDateTime.ToString("g"));
            output.KeyValue("Commands", stateService.CurrentState.CommandCount.ToString());
            return ExitCodes.Success;
        }
        else
        {
            output.Error($"Session not found: {sessionIdOrName}");
            return ExitCodes.ConfigurationError;
        }
    }
    catch (Exception ex)
    {
        output.Error($"Failed to load session: {ex.Message}");
        return ExitCodes.ConfigurationError;
    }
});
replSessionCommand.Subcommands.Add(replSessionLoadCommand);

// repl-session list
var replSessionListCommand = new Command("list", "List saved REPL sessions");
replSessionListCommand.SetAction(async parseResult =>
{
    try
    {
        var sessions = await sessionStore.ListAsync();

        if (sessions.Count == 0)
        {
            output.Info("No saved sessions.");
            return ExitCodes.Success;
        }

        var tableConfig = new TableConfig<SessionSummary>
        {
            Columns = new List<TableColumn<SessionSummary>>
            {
                new() { Header = "ID", Selector = s => s.SessionId },
                new() { Header = "Name", Selector = s => s.Name ?? "-" },
                new() { Header = "Started", Selector = s => s.StartedAt.LocalDateTime.ToString("g") },
                new() { Header = "Saved", Selector = s => s.SavedAt.LocalDateTime.ToString("g") },
                new() { Header = "Commands", Selector = s => s.CommandCount.ToString() }
            }
        };

        output.Table(sessions, tableConfig);
        return ExitCodes.Success;
    }
    catch (Exception ex)
    {
        output.Error($"Failed to list sessions: {ex.Message}");
        return ExitCodes.ConfigurationError;
    }
});
replSessionCommand.Subcommands.Add(replSessionListCommand);

// repl-session delete
var deleteSessionArg = new Argument<string>("session")
{
    Description = "Session ID or name to delete"
};
var replSessionDeleteCommand = new Command("delete", "Delete a saved REPL session");
replSessionDeleteCommand.Arguments.Add(deleteSessionArg);
replSessionDeleteCommand.SetAction(async parseResult =>
{
    var sessionIdOrName = parseResult.GetValue(deleteSessionArg);

    try
    {
        var deleted = await sessionStore.DeleteAsync(sessionIdOrName!);
        if (deleted)
        {
            output.Success($"Session deleted: {sessionIdOrName}");
            return ExitCodes.Success;
        }
        else
        {
            output.Error($"Session not found: {sessionIdOrName}");
            return ExitCodes.ConfigurationError;
        }
    }
    catch (Exception ex)
    {
        output.Error($"Failed to delete session: {ex.Message}");
        return ExitCodes.ConfigurationError;
    }
});
replSessionCommand.Subcommands.Add(replSessionDeleteCommand);

rootCommand.Subcommands.Add(replSessionCommand);

// REPL command
var replCommand = new Command("repl", "Start interactive REPL mode");

var noHeaderReplOption = new Option<bool>("--no-header")
{
    Description = "Suppress welcome header",
    DefaultValueFactory = _ => false
};
replCommand.Options.Add(noHeaderReplOption);

replCommand.SetAction(async parseResult =>
{
    var noHeader = parseResult.GetValue(noHeaderReplOption);
    
    // Set up auto-completer with available commands
    var autoCompleter = new CommandAutoCompleter();
    autoCompleter.RegisterCommand("version", "Display version information", options: ["--format", "-f"]);
    autoCompleter.RegisterCommand("help", "Display help information", options: ["--format", "-f"]);
    autoCompleter.RegisterCommand("auth", "Authentication commands", 
        subcommands: ["login", "logout", "status"], 
        options: ["--token"]);
    autoCompleter.RegisterCommand("chat", "Start AI chat session",
        options: ["--model", "-m", "--streaming", "-s", "--resume", "-r", "--no-header"]);
    autoCompleter.RegisterCommand("sessions", "Manage chat sessions",
        subcommands: ["list", "delete"]);
    autoCompleter.RegisterCommand("loop", "Autonomous development workflow",
        subcommands: ["configure"],
        options: ["--auto", "-a", "--config", "-c", "--no-header"]);
    autoCompleter.RegisterCommand("repl", "Start interactive REPL mode",
        options: ["--no-header"]);
    autoCompleter.RegisterCommand("repl-session", "Manage REPL session state",
        subcommands: ["save", "load", "list", "delete"]);
    autoCompleter.RegisterCommand("exit", "Exit the REPL");
    autoCompleter.RegisterCommand("quit", "Exit the REPL");
    
    // Set up command history with persistence
    var history = new PersistentCommandHistory();
    var consoleInput = new ConsoleInputWithHistory(history, autoCompleter);
    
    // Set up session state
    var sessionStateService = new SessionStateService(authService, sessionStore);
    
    var replService = new ReplService(consoleInput, output, sessionStateService);
    
    // Show welcome header unless suppressed
    if (!noHeader)
    {
        ShowWelcomeHeader();
    }
    
    return await replService.RunAsync(async cmdArgs =>
    {
        // Execute command using the root command parser
        var result = rootCommand.Parse(cmdArgs);
        return await result.InvokeAsync();
    });
});
rootCommand.Subcommands.Add(replCommand);

// Loop command
var loopCommand = new Command("loop", "Autonomous development workflow");

var loopAutoOption = new Option<bool>("--auto")
{
    Description = "Skip interactive setup, use defaults",
    DefaultValueFactory = _ => false
};
loopAutoOption.Aliases.Add("-a");

var loopConfigOption = new Option<string?>("--config")
{
    Description = "Path to custom config file"
};
loopConfigOption.Aliases.Add("-c");

var noHeaderLoopOption = new Option<bool>("--no-header")
{
    Description = "Suppress welcome header",
    DefaultValueFactory = _ => false
};

loopCommand.Options.Add(loopAutoOption);
loopCommand.Options.Add(loopConfigOption);
loopCommand.Options.Add(noHeaderLoopOption);

loopCommand.SetAction(async parseResult =>
{
    var auto = parseResult.GetValue(loopAutoOption);
    var configPath = parseResult.GetValue(loopConfigOption);
    var noHeader = parseResult.GetValue(noHeaderLoopOption);

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

    // Load configuration
    var configService = new LoopConfigService();
    var config = await configService.LoadConfigAsync(configPath);

    var stateManager = new LoopStateManager();
    var loopOutput = new LoopOutputService(output);
    var verificationService = new VerificationService(copilotService);

    bool skipPlan = false;
    bool skipBuild = false;

    if (!noHeader)
    {
        ShowWelcomeHeader();
    }

    if (!auto)
    {
        output.Info("Lopen Loop - Autonomous Development Workflow");
        output.WriteLine();
        output.WriteLine("Options:");
        output.WriteLine("  1. Add specifications, then plan and build");
        output.WriteLine("  2. Proceed to planning phase");
        output.WriteLine("  3. Skip planning, start building");
        output.Write("Select (1-3, default=2): ");

        var choice = Console.ReadLine()?.Trim();
        switch (choice)
        {
            case "1":
                output.Info("Add specifications to docs/requirements/, then re-run loop.");
                return ExitCodes.Success;
            case "3":
                skipPlan = true;
                break;
            case "2":
            case "":
            default:
                // Default: plan then build
                break;
        }
    }

    var loopService = new LoopService(copilotService, stateManager, loopOutput, config, verificationService);

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    return await loopService.RunAsync(skipPlan, skipBuild, cts.Token);
});

// loop configure subcommand
var configureCommand = new Command("configure", "Configure loop settings");

var configModelOption = new Option<string?>("--model")
{
    Description = "AI model to use"
};
configModelOption.Aliases.Add("-m");

var configPlanPromptOption = new Option<string?>("--plan-prompt")
{
    Description = "Path to plan prompt file"
};

var configBuildPromptOption = new Option<string?>("--build-prompt")
{
    Description = "Path to build prompt file"
};

var configResetOption = new Option<bool>("--reset")
{
    Description = "Reset configuration to defaults",
    DefaultValueFactory = _ => false
};

configureCommand.Options.Add(configModelOption);
configureCommand.Options.Add(configPlanPromptOption);
configureCommand.Options.Add(configBuildPromptOption);
configureCommand.Options.Add(configResetOption);

configureCommand.SetAction(async parseResult =>
{
    var model = parseResult.GetValue(configModelOption);
    var planPrompt = parseResult.GetValue(configPlanPromptOption);
    var buildPrompt = parseResult.GetValue(configBuildPromptOption);
    var reset = parseResult.GetValue(configResetOption);

    var configService = new LoopConfigService();

    if (reset)
    {
        await configService.ResetUserConfigAsync();
        output.Success("Configuration reset to defaults.");
        return ExitCodes.Success;
    }

    var config = await configService.LoadConfigAsync();
    
    // If no flags provided and in interactive terminal, show interactive prompts
    var noFlagsProvided = string.IsNullOrEmpty(model) && 
                          string.IsNullOrEmpty(planPrompt) && 
                          string.IsNullOrEmpty(buildPrompt);
    
    if (noFlagsProvided && !Console.IsInputRedirected)
    {
        var interactiveConfig = new SpectreInteractiveLoopConfigService();
        var result = interactiveConfig.PromptForConfiguration(config);
        
        if (result.Cancelled)
        {
            output.Muted("Configuration cancelled.");
            return ExitCodes.Success;
        }
        
        config = result.Config!;
    }
    else
    {
        // Apply any specified options
        if (!string.IsNullOrEmpty(model))
        {
            config = config with { Model = model };
        }
        if (!string.IsNullOrEmpty(planPrompt))
        {
            config = config with { PlanPromptPath = planPrompt };
        }
        if (!string.IsNullOrEmpty(buildPrompt))
        {
            config = config with { BuildPromptPath = buildPrompt };
        }
    }

    await configService.SaveUserConfigAsync(config);
    output.Success("Configuration saved.");
    output.KeyValue("Model", config.Model);
    output.KeyValue("Plan Prompt", config.PlanPromptPath);
    output.KeyValue("Build Prompt", config.BuildPromptPath);
    output.KeyValue("Stream", config.Stream.ToString());

    return ExitCodes.Success;
});

loopCommand.Subcommands.Add(configureCommand);
rootCommand.Subcommands.Add(loopCommand);

// Test command
var testCommand = new Command("test", "Testing commands");

// test self
var testSelfCommand = new Command("self", "Run self-tests");

var testVerboseOption = new Option<bool>("--verbose")
{
    Description = "Show detailed output per test",
    DefaultValueFactory = _ => false
};
testVerboseOption.Aliases.Add("-v");

var testFilterOption = new Option<string?>("--filter")
{
    Description = "Filter tests by pattern (matches ID, suite, or description)"
};

var testModelOption = new Option<string>("--model")
{
    Description = "AI model to use for tests",
    DefaultValueFactory = _ => "gpt-5-mini"
};
testModelOption.Aliases.Add("-m");

var testTimeoutOption = new Option<int>("--timeout")
{
    Description = "Per-test timeout in seconds",
    DefaultValueFactory = _ => 30
};
testTimeoutOption.Aliases.Add("-t");

var testFormatOption = new Option<string>("--format")
{
    Description = "Output format (text, json)",
    DefaultValueFactory = _ => "text"
};
testFormatOption.Aliases.Add("-f");
testFormatOption.AcceptOnlyFromAmong("text", "json");

var testInteractiveOption = new Option<bool>("--interactive")
{
    Description = "Interactive suite/test selection mode",
    DefaultValueFactory = _ => false
};
testInteractiveOption.Aliases.Add("-i");

testSelfCommand.Options.Add(testVerboseOption);
testSelfCommand.Options.Add(testFilterOption);
testSelfCommand.Options.Add(testModelOption);
testSelfCommand.Options.Add(testTimeoutOption);
testSelfCommand.Options.Add(testFormatOption);
testSelfCommand.Options.Add(testInteractiveOption);

testSelfCommand.SetAction(async parseResult =>
{
    var verbose = parseResult.GetValue(testVerboseOption);
    var filter = parseResult.GetValue(testFilterOption);
    var model = parseResult.GetValue(testModelOption);
    var timeout = parseResult.GetValue(testTimeoutOption);
    var format = parseResult.GetValue(testFormatOption);
    var interactive = parseResult.GetValue(testInteractiveOption);
    
    // Get tests (apply filter if specified)
    var tests = string.IsNullOrEmpty(filter)
        ? Lopen.Core.Testing.TestSuites.TestSuiteRegistry.GetAllTests().ToList()
        : Lopen.Core.Testing.TestSuites.TestSuiteRegistry.FilterByPattern(filter).ToList();
    
    if (tests.Count == 0)
    {
        output.Warning("No tests match the specified filter.");
        return ExitCodes.Success;
    }
    
    // Set up cancellation
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };
    
    // Interactive mode: prompt for test/model selection
    if (interactive)
    {
        if (!Console.IsInputRedirected)
        {
            var selector = new Lopen.Core.Testing.SpectreInteractiveTestSelector();
            var selection = selector.SelectTests(tests, model!, cts.Token);
            
            if (selection.Cancelled)
            {
                output.Muted("Selection cancelled.");
                return ExitCodes.Success;
            }
            
            tests = selection.Tests.ToList();
            model = selection.Model;
            
            if (tests.Count == 0)
            {
                output.Warning("No tests selected.");
                return ExitCodes.Success;
            }
        }
        else
        {
            output.Warning("Interactive mode requires a terminal. Running all matching tests.");
        }
    }
    
    var context = new Lopen.Core.Testing.TestContext
    {
        Model = model!,
        Timeout = TimeSpan.FromSeconds(timeout),
        Verbose = verbose
    };
    
    var testOutput = new Lopen.Core.Testing.TestOutputService(output);
    
    // Display header (unless JSON output)
    if (format != "json")
    {
        testOutput.DisplayHeader(model!, tests.Count);
    }
    
    // Run tests
    // Use progress bar in non-verbose mode when terminal is interactive
    IProgressRenderer? testProgressRenderer = null;
    if (!verbose && format != "json" && !Console.IsInputRedirected)
    {
        testProgressRenderer = progressRenderer;
    }
    var runner = new Lopen.Core.Testing.TestRunner(progressRenderer: testProgressRenderer);
    var summary = await runner.RunTestsAsync(
        tests,
        context,
        progressCallback: verbose && format != "json" ? result => testOutput.DisplayVerboseResult(result) : null,
        cancellationToken: cts.Token);
    
    // Output results
    if (format == "json")
    {
        Console.WriteLine(testOutput.FormatAsJson(summary));
    }
    else
    {
        if (!verbose)
        {
            testOutput.DisplayResults(summary);
        }
        testOutput.DisplaySummary(summary);
    }
    
    return summary.AllPassed ? ExitCodes.Success : ExitCodes.GeneralError;
});

testCommand.Subcommands.Add(testSelfCommand);
rootCommand.Subcommands.Add(testCommand);

// Set action for root command (when no subcommand given)
rootCommand.SetAction(parseResult =>
{
    Console.WriteLine("Use --help for available commands or 'lopen repl' for interactive mode");
    return 0;
});

// Parse and check for errors before invoking
var parseResult = rootCommand.Parse(args);

if (parseResult.Errors.Any())
{
    // Get list of available commands for suggestions
    var availableCommands = rootCommand.Subcommands.Select(c => c.Name).ToList();
    var errorHandler = new CommandLineErrorHandler(errorRenderer, availableCommands);
    
    // Convert System.CommandLine errors to ParseErrorInfo
    var errors = parseResult.Errors.Select(e => new ParseErrorInfo(e.Message));
    var commandTokens = parseResult.Tokens
        .Where(t => t.Type == System.CommandLine.Parsing.TokenType.Argument || 
                    t.Type == System.CommandLine.Parsing.TokenType.Command)
        .Select(t => t.Value);
    
    return errorHandler.HandleParseErrors(errors, commandTokens);
}

return parseResult.Invoke();
