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
/// Console input with command history support (Up/Down arrow navigation).
/// </summary>
public interface IConsoleInputWithHistory : IConsoleInput
{
    /// <summary>
    /// Gets the command history.
    /// </summary>
    ICommandHistory History { get; }
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

/// <summary>
/// Console input with command history navigation using Up/Down arrow keys.
/// </summary>
public class ConsoleInputWithHistory : IConsoleInputWithHistory
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ICommandHistory _history;

    public ConsoleInputWithHistory(ICommandHistory history)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _cts.Cancel();
        };
    }

    public ICommandHistory History => _history;

    public CancellationToken CancellationToken => _cts.Token;

    public string? ReadLine()
    {
        var buffer = new List<char>();
        var cursorPos = 0;

        _history.ResetPosition();

        while (true)
        {
            if (_cts.Token.IsCancellationRequested)
                throw new OperationCanceledException();

            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    var result = new string(buffer.ToArray());
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        _history.Add(result);
                    }
                    return result;

                case ConsoleKey.Backspace:
                    if (cursorPos > 0)
                    {
                        buffer.RemoveAt(cursorPos - 1);
                        cursorPos--;
                        RedrawLine(buffer, cursorPos);
                    }
                    break;

                case ConsoleKey.Delete:
                    if (cursorPos < buffer.Count)
                    {
                        buffer.RemoveAt(cursorPos);
                        RedrawLine(buffer, cursorPos);
                    }
                    break;

                case ConsoleKey.LeftArrow:
                    if (cursorPos > 0)
                    {
                        cursorPos--;
                        Console.CursorLeft--;
                    }
                    break;

                case ConsoleKey.RightArrow:
                    if (cursorPos < buffer.Count)
                    {
                        cursorPos++;
                        Console.CursorLeft++;
                    }
                    break;

                case ConsoleKey.Home:
                    Console.CursorLeft -= cursorPos;
                    cursorPos = 0;
                    break;

                case ConsoleKey.End:
                    Console.CursorLeft += buffer.Count - cursorPos;
                    cursorPos = buffer.Count;
                    break;

                case ConsoleKey.UpArrow:
                    var prev = _history.GetPrevious();
                    if (prev != null)
                    {
                        ReplaceBuffer(buffer, prev, ref cursorPos);
                    }
                    break;

                case ConsoleKey.DownArrow:
                    var next = _history.GetNext();
                    ReplaceBuffer(buffer, next ?? "", ref cursorPos);
                    break;

                case ConsoleKey.Escape:
                    // Clear current input
                    ReplaceBuffer(buffer, "", ref cursorPos);
                    break;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        buffer.Insert(cursorPos, key.KeyChar);
                        cursorPos++;
                        if (cursorPos == buffer.Count)
                        {
                            Console.Write(key.KeyChar);
                        }
                        else
                        {
                            RedrawLine(buffer, cursorPos);
                        }
                    }
                    break;
            }
        }
    }

    private void RedrawLine(List<char> buffer, int cursorPos)
    {
        // Save current position, clear from start of input, redraw, restore cursor
        var lineStart = Console.CursorLeft - cursorPos;
        if (lineStart < 0) lineStart = 0;

        Console.CursorLeft = lineStart;
        Console.Write(new string(' ', buffer.Count + 10)); // Clear extra space
        Console.CursorLeft = lineStart;
        Console.Write(new string(buffer.ToArray()));
        Console.CursorLeft = lineStart + cursorPos;
    }

    private void ReplaceBuffer(List<char> buffer, string newContent, ref int cursorPos)
    {
        // Clear current line
        var lineStart = Console.CursorLeft - cursorPos;
        if (lineStart < 0) lineStart = 0;

        Console.CursorLeft = lineStart;
        Console.Write(new string(' ', buffer.Count + 10));
        Console.CursorLeft = lineStart;

        // Replace buffer
        buffer.Clear();
        buffer.AddRange(newContent);
        cursorPos = buffer.Count;

        Console.Write(newContent);
    }
}

