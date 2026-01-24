namespace Lopen.Core;

/// <summary>
/// REPL service for interactive command execution.
/// </summary>
public class ReplService
{
    private readonly IConsoleInput _input;
    private readonly ConsoleOutput _output;
    private readonly ISessionStateService? _sessionStateService;
    private readonly string _prompt;

    /// <summary>
    /// Exit commands that terminate the REPL.
    /// </summary>
    private static readonly HashSet<string> ExitCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "exit",
        "quit"
    };

    public ReplService(IConsoleInput input, ConsoleOutput output, string prompt = "lopen> ")
        : this(input, output, null, prompt)
    {
    }

    public ReplService(IConsoleInput input, ConsoleOutput output, ISessionStateService? sessionStateService, string prompt = "lopen> ")
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _sessionStateService = sessionStateService;
        _prompt = prompt;
    }

    /// <summary>
    /// Gets the current session state, if available.
    /// </summary>
    public SessionState? SessionState => _sessionStateService?.CurrentState;

    /// <summary>
    /// Runs the REPL loop.
    /// </summary>
    /// <param name="commandExecutor">Function to execute commands. Receives args array, returns exit code.</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>Exit code (0 for success).</returns>
    public async Task<int> RunAsync(
        Func<string[], Task<int>> commandExecutor,
        CancellationToken cancellationToken = default)
    {
        // Initialize session state if available
        if (_sessionStateService is not null)
        {
            await _sessionStateService.InitializeAsync();
        }

        // Combine external cancellation with Ctrl+C
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _input.CancellationToken);

        _output.Info("REPL started. Type 'exit' or 'quit' to exit.");

        while (!linkedCts.Token.IsCancellationRequested)
        {
            // Write prompt
            _output.Write(_prompt);

            // Read input
            string? line;
            try
            {
                line = _input.ReadLine();
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // EOF (Ctrl+D)
            if (line is null)
            {
                _output.WriteLine();
                break;
            }

            // Skip empty lines
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            // Check for exit commands
            if (ExitCommands.Contains(trimmed))
            {
                break;
            }

            // Record command in session state
            _sessionStateService?.RecordCommand(trimmed);

            // Parse and execute command
            var args = ParseArgs(trimmed);
            try
            {
                await commandExecutor(args);
            }
            catch (Exception ex)
            {
                _output.Error($"Command failed: {ex.Message}");
            }
        }

        _output.Info("Goodbye!");
        return 0;
    }

    /// <summary>
    /// Parse input line into args array.
    /// Simple implementation - splits on whitespace.
    /// TODO: Handle quoted strings for future enhancement.
    /// </summary>
    private static string[] ParseArgs(string input)
    {
        return input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}
