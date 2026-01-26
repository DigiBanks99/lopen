namespace Lopen.Core;

/// <summary>
/// Service for streaming loop output with phase indicators.
/// </summary>
public class LoopOutputService
{
    private readonly ConsoleOutput _output;
    private int _iterationCount;

    /// <summary>
    /// Creates a new LoopOutputService.
    /// </summary>
    public LoopOutputService(ConsoleOutput output)
    {
        _output = output;
    }

    /// <summary>
    /// Current iteration count.
    /// </summary>
    public int IterationCount => _iterationCount;

    /// <summary>
    /// Write a phase header (PLAN or BUILD).
    /// </summary>
    public void WritePhaseHeader(string phase)
    {
        _output.WriteLine();
        _output.Rule(phase);
        _output.WriteLine();
    }

    /// <summary>
    /// Write an iteration completion message.
    /// </summary>
    public void WriteIterationComplete()
    {
        _iterationCount++;
        _output.WriteLine();
        _output.Rule($"Completed iteration {_iterationCount}");
        _output.WriteLine();
    }

    /// <summary>
    /// Write a streaming chunk of output.
    /// </summary>
    public void WriteChunk(string chunk)
    {
        Console.Write(chunk);
    }

    /// <summary>
    /// Write a newline.
    /// </summary>
    public void WriteLine()
    {
        Console.WriteLine();
    }

    /// <summary>
    /// Write an info message.
    /// </summary>
    public void Info(string message)
    {
        _output.Info(message);
    }

    /// <summary>
    /// Write a success message.
    /// </summary>
    public void Success(string message)
    {
        _output.Success(message);
    }

    /// <summary>
    /// Write an error message.
    /// </summary>
    public void Error(string message)
    {
        _output.Error(message);
    }

    /// <summary>
    /// Write a warning message.
    /// </summary>
    public void Warning(string message)
    {
        _output.Warning(message);
    }

    /// <summary>
    /// Write a muted message.
    /// </summary>
    public void Muted(string message)
    {
        _output.Muted(message);
    }

    /// <summary>
    /// Reset iteration counter.
    /// </summary>
    public void ResetIterationCount()
    {
        _iterationCount = 0;
    }
}
