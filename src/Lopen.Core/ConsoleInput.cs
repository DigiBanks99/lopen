namespace Lopen.Core;

/// <summary>
/// Abstraction for console input to enable testing.
/// </summary>
public interface IConsoleInput
{
    /// <summary>
    /// Reads a line of input from the console.
    /// Returns null on EOF (Ctrl+D).
    /// </summary>
    string? ReadLine();

    /// <summary>
    /// Gets the cancellation token that is signaled on Ctrl+C.
    /// </summary>
    CancellationToken CancellationToken { get; }
}

/// <summary>
/// Default console input implementation using Console.ReadLine.
/// </summary>
public class DefaultConsoleInput : IConsoleInput
{
    private readonly CancellationTokenSource _cts = new();

    public DefaultConsoleInput()
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // Prevent immediate termination
            _cts.Cancel();
        };
    }

    public string? ReadLine() => Console.ReadLine();

    public CancellationToken CancellationToken => _cts.Token;
}
