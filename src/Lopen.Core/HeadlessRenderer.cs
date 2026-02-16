namespace Lopen.Core;

/// <summary>
/// Plain text output renderer for headless (non-TUI) mode.
/// Writes structured progress to stdout and errors to stderr.
/// </summary>
public sealed class HeadlessRenderer : IOutputRenderer
{
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    public HeadlessRenderer(TextWriter? stdout = null, TextWriter? stderr = null)
    {
        _stdout = stdout ?? Console.Out;
        _stderr = stderr ?? Console.Error;
    }

    public async Task RenderProgressAsync(string phase, string step, double progress, CancellationToken cancellationToken = default)
    {
        var pct = progress >= 0 ? $" ({progress:P0})" : "";
        await _stdout.WriteLineAsync($"[{phase}] {step}{pct}");
    }

    public async Task RenderErrorAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        await _stderr.WriteLineAsync($"Error: {message}");
        if (exception is not null)
        {
            await _stderr.WriteLineAsync($"  {exception.GetType().Name}: {exception.Message}");
        }
    }

    public async Task RenderResultAsync(string message, CancellationToken cancellationToken = default)
    {
        await _stdout.WriteLineAsync(message);
    }

    public Task<string?> PromptAsync(string message, CancellationToken cancellationToken = default)
    {
        // Headless mode is non-interactive; return null.
        return Task.FromResult<string?>(null);
    }
}
