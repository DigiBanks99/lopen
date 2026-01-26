using Spectre.Console;

namespace Lopen.Core;

/// <summary>
/// Spectre.Console implementation of progress renderer with spinners.
/// Respects NO_COLOR environment variable.
/// </summary>
public class SpectreProgressRenderer : IProgressRenderer
{
    private readonly IAnsiConsole _console;
    private readonly bool _useColors;
    private readonly SpinnerType _spinnerType;

    public SpectreProgressRenderer()
        : this(AnsiConsole.Console, SpinnerType.Dots)
    {
    }

    public SpectreProgressRenderer(IAnsiConsole console, SpinnerType spinnerType = SpinnerType.Dots)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _spinnerType = spinnerType;
        _useColors = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
    }

    public async Task<T> ShowProgressAsync<T>(
        string status,
        Func<IProgressContext, Task<T>> operation,
        CancellationToken ct = default)
    {
        if (!_useColors || !_console.Profile.Capabilities.Interactive)
        {
            return await ShowProgressTextOnlyAsync(status, operation, ct);
        }

        T result = default!;
        await _console.Status()
            .Spinner(GetSpinner())
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync(status, async ctx =>
            {
                var progressContext = new SpectreProgressContext(ctx);
                result = await operation(progressContext);
            });

        return result;
    }

    public async Task ShowProgressAsync(
        string status,
        Func<IProgressContext, Task> operation,
        CancellationToken ct = default)
    {
        await ShowProgressAsync<object?>(status, async ctx =>
        {
            await operation(ctx);
            return null;
        }, ct);
    }

    private async Task<T> ShowProgressTextOnlyAsync<T>(
        string status,
        Func<IProgressContext, Task<T>> operation,
        CancellationToken ct)
    {
        _console.WriteLine($"⏳ {status}");
        var progressContext = new TextOnlyProgressContext(_console);
        var result = await operation(progressContext);
        _console.WriteLine("✓ Done");
        return result;
    }

    private Spinner GetSpinner() => _spinnerType switch
    {
        SpinnerType.Arc => Spinner.Known.Arc,
        SpinnerType.Line => Spinner.Known.Line,
        SpinnerType.SimpleDotsScrolling => Spinner.Known.SimpleDotsScrolling,
        _ => Spinner.Known.Dots
    };

    private sealed class SpectreProgressContext : IProgressContext
    {
        private readonly StatusContext _ctx;

        public SpectreProgressContext(StatusContext ctx) => _ctx = ctx;

        public void UpdateStatus(string status) => _ctx.Status(status);
    }

    private sealed class TextOnlyProgressContext : IProgressContext
    {
        private readonly IAnsiConsole _console;

        public TextOnlyProgressContext(IAnsiConsole console) => _console = console;

        public void UpdateStatus(string status) => _console.WriteLine($"  → {status}");
    }
}
