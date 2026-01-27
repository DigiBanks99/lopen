using Spectre.Console;

namespace Lopen.Core.Testing;

/// <summary>
/// Spectre.Console implementation of interactive prompts for tests.
/// </summary>
public class SpectreInteractivePrompt : IInteractivePrompt
{
    private readonly IAnsiConsole _console;
    private readonly bool _useColors;

    public SpectreInteractivePrompt()
        : this(AnsiConsole.Console)
    {
    }

    public SpectreInteractivePrompt(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _useColors = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
    }

    /// <inheritdoc/>
    public void DisplayMessage(string message)
    {
        _console.WriteLine();
        if (_useColors)
        {
            _console.MarkupLine($"[blue]{Markup.Escape(message)}[/]");
        }
        else
        {
            _console.WriteLine(message);
        }
    }

    /// <inheritdoc/>
    public void DisplayStep(int stepNumber, int totalSteps, string instruction)
    {
        _console.WriteLine();
        if (_useColors)
        {
            _console.MarkupLine($"[bold yellow]Step {stepNumber}/{totalSteps}:[/] {Markup.Escape(instruction)}");
        }
        else
        {
            _console.WriteLine($"Step {stepNumber}/{totalSteps}: {instruction}");
        }
    }

    /// <inheritdoc/>
    public bool Confirm(string prompt)
    {
        return _console.Confirm(prompt);
    }

    /// <inheritdoc/>
    public bool ConfirmSuccess(string prompt)
    {
        _console.WriteLine();
        if (_useColors)
        {
            _console.MarkupLine("[bold]Verification Required[/]");
        }
        else
        {
            _console.WriteLine("Verification Required");
        }
        return _console.Confirm(prompt);
    }

    /// <inheritdoc/>
    public void WaitForContinue(string message = "Press any key to continue...")
    {
        _console.WriteLine();
        if (_useColors)
        {
            _console.MarkupLine($"[dim]{Markup.Escape(message)}[/]");
        }
        else
        {
            _console.WriteLine(message);
        }
        _console.Input.ReadKey(intercept: true);
    }
}

/// <summary>
/// Mock implementation of interactive prompts for testing.
/// </summary>
public class MockInteractivePrompt : IInteractivePrompt
{
    private readonly List<string> _messages = new();
    private readonly List<(int Step, int Total, string Instruction)> _steps = new();
    private readonly Queue<bool> _confirmResponses = new();
    private readonly Queue<bool> _successResponses = new();
    private int _waitCount;

    /// <summary>Gets all messages displayed.</summary>
    public IReadOnlyList<string> Messages => _messages.AsReadOnly();

    /// <summary>Gets all steps displayed.</summary>
    public IReadOnlyList<(int Step, int Total, string Instruction)> Steps => _steps.AsReadOnly();

    /// <summary>Gets the number of times WaitForContinue was called.</summary>
    public int WaitCount => _waitCount;

    /// <summary>Queue a confirm response.</summary>
    public void QueueConfirmResponse(bool response) => _confirmResponses.Enqueue(response);

    /// <summary>Queue a success response.</summary>
    public void QueueSuccessResponse(bool response) => _successResponses.Enqueue(response);

    /// <inheritdoc/>
    public void DisplayMessage(string message) => _messages.Add(message);

    /// <inheritdoc/>
    public void DisplayStep(int stepNumber, int totalSteps, string instruction)
        => _steps.Add((stepNumber, totalSteps, instruction));

    /// <inheritdoc/>
    public bool Confirm(string prompt)
        => _confirmResponses.Count > 0 ? _confirmResponses.Dequeue() : true;

    /// <inheritdoc/>
    public bool ConfirmSuccess(string prompt)
        => _successResponses.Count > 0 ? _successResponses.Dequeue() : true;

    /// <inheritdoc/>
    public void WaitForContinue(string message = "Press any key to continue...")
        => _waitCount++;
}
