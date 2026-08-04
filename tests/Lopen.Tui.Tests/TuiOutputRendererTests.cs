using Lopen.Llm;
using Spectre.Console;

namespace Lopen.Tui.Tests;

public class TuiOutputRendererTests
{
    [Fact]
    public void Constructor_ThrowsOnNullConsole()
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        FileLineEditorHistory history = new(
            Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid():N}", "history.txt"));
        LopenLineEditor editor = new(console, history);

        Assert.Throws<ArgumentNullException>(() => new TuiOutputRenderer(null!, new Lazy<LopenLineEditor>(() => editor)));
    }

    [Fact]
    public void Constructor_ThrowsOnNullEditor()
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });

        Assert.Throws<ArgumentNullException>(() => new TuiOutputRenderer(console, null!));
    }

    [Fact]
    public async Task RenderProgressAsync_DoesNotThrow()
    {
        (TuiOutputRenderer? renderer, IAnsiConsole _) = CreateRenderer();
        await renderer.RenderProgressAsync("Build", "compiling", 0.5);
    }

    [Fact]
    public async Task RenderErrorAsync_DoesNotThrow()
    {
        (TuiOutputRenderer? renderer, IAnsiConsole _) = CreateRenderer();
        await renderer.RenderErrorAsync("Something went wrong", new InvalidOperationException("test"));
    }

    [Fact]
    public async Task RenderResultAsync_DoesNotThrow()
    {
        (TuiOutputRenderer? renderer, IAnsiConsole _) = CreateRenderer();
        await renderer.RenderResultAsync("Operation complete");
    }

    [Fact]
    public async Task RenderErrorAsync_LlmExceptionWithDiagnostics_RendersWithoutThrowing()
    {
        (TuiOutputRenderer renderer, _) = CreateRenderer();
        var inner = new InvalidOperationException("401 Unauthorized");
        var llmEx = new LlmException("Failed to start Copilot SDK client", model: null, inner)
        {
            DiagnosticCategory = CopilotFailureCategory.Auth,
            UserHint = "Run 'lopen auth login' to re-authenticate.",
        };

        // Should render category and hint without throwing
        await renderer.RenderErrorAsync("Failed to start Copilot SDK client", llmEx);
    }

    [Fact]
    public async Task RenderErrorAsync_LlmExceptionWithDiagnostics_WritesOutputIncludingHint()
    {
        (TuiOutputRenderer renderer, StringWriter writer) = CreateCapturingRenderer();

        var inner = new InvalidOperationException("401 Unauthorized");
        var llmEx = new LlmException("Failed to start Copilot SDK client", model: null, inner)
        {
            DiagnosticCategory = CopilotFailureCategory.Auth,
            UserHint = "Run 'lopen auth login' to re-authenticate.",
        };

        await renderer.RenderErrorAsync("Failed to start Copilot SDK client", llmEx);

        string output = writer.ToString();
        Assert.Contains("Auth", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lopen auth login", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenderErrorAsync_LlmExceptionNetworkCategory_WritesNetworkHint()
    {
        (TuiOutputRenderer renderer, StringWriter writer) = CreateCapturingRenderer();

        var inner = new InvalidOperationException("Connection timeout");
        var llmEx = new LlmException("Failed to start Copilot SDK client", model: null, inner)
        {
            DiagnosticCategory = CopilotFailureCategory.Network,
            UserHint = "Check your network connection and verify the Copilot service is reachable.",
        };

        await renderer.RenderErrorAsync("Failed to start Copilot SDK client", llmEx);

        string output = writer.ToString();
        Assert.Contains("Network", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("network connection", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenderErrorAsync_PlainException_RendersExceptionTypeAndMessage()
    {
        (TuiOutputRenderer renderer, StringWriter writer) = CreateCapturingRenderer();

        var ex = new InvalidOperationException("Something failed");
        await renderer.RenderErrorAsync("An error occurred", ex);

        string output = writer.ToString();
        Assert.Contains("InvalidOperationException", output);
        Assert.Contains("Something failed", output);
    }

    [Fact]
    public async Task RenderErrorAsync_LlmExceptionWithoutDiagnostics_FallsBackToExceptionDetails()
    {
        (TuiOutputRenderer renderer, StringWriter writer) = CreateCapturingRenderer();

        var llmEx = new LlmException("Rate limited", "gpt-5-mini");
        await renderer.RenderErrorAsync("LLM error", llmEx);

        string output = writer.ToString();
        Assert.Contains("LlmException", output);
        Assert.Contains("Rate limited", output);
    }

    [Fact]
    public void ImplementsIOutputRenderer()
    {
        (TuiOutputRenderer? renderer, IAnsiConsole _) = CreateRenderer();
        Assert.IsAssignableFrom<Lopen.Core.IOutputRenderer>(renderer);
    }

    private static (TuiOutputRenderer renderer, IAnsiConsole console) CreateRenderer()
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        FileLineEditorHistory history = new(
            Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid():N}", "history.txt"));
        LopenLineEditor editor = new(console, history);
        return (new TuiOutputRenderer(console, new Lazy<LopenLineEditor>(() => editor)), console);
    }

    /// <summary>
    /// Creates a renderer that captures output to a <see cref="StringWriter"/>.
    /// The <see cref="LopenLineEditor"/> is never instantiated since these tests only
    /// call <see cref="TuiOutputRenderer.RenderErrorAsync"/> which does not need it.
    /// </summary>
    private static (TuiOutputRenderer renderer, StringWriter writer) CreateCapturingRenderer()
    {
        var writer = new StringWriter();
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(writer),
        });
        Lazy<LopenLineEditor> neverUsedEditor = new(
            () => throw new NotSupportedException("LopenLineEditor is not needed for RenderErrorAsync tests."));
        return (new TuiOutputRenderer(console, neverUsedEditor), writer);
    }
}
