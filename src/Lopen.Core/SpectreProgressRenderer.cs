using Spectre.Console;

namespace Lopen.Core;

/// <summary>
/// Spectre.Console implementation of progress renderer with spinners and progress bars.
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

    public async Task ShowProgressBarAsync(
        string description,
        int totalCount,
        Func<IProgressBarContext, Task> operation,
        CancellationToken ct = default)
    {
        if (totalCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount), "Total count must be positive.");
        }

        if (!_useColors || !_console.Profile.Capabilities.Interactive)
        {
            await ShowProgressBarTextOnlyAsync(description, totalCount, operation, ct);
            return;
        }

        await _console.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask(description, maxValue: totalCount);
                var progressContext = new SpectreProgressBarContext(task);
                await operation(progressContext);
            });
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

    private async Task ShowProgressBarTextOnlyAsync(
        string description,
        int totalCount,
        Func<IProgressBarContext, Task> operation,
        CancellationToken ct)
    {
        _console.WriteLine($"⏳ {description} (0/{totalCount})");
        var progressContext = new TextOnlyProgressBarContext(_console, description, totalCount);
        await operation(progressContext);
        _console.WriteLine($"✓ {description} complete");
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

    private sealed class SpectreProgressBarContext : IProgressBarContext
    {
        private readonly ProgressTask _task;

        public SpectreProgressBarContext(ProgressTask task) => _task = task;

        public void Increment(int amount = 1) => _task.Increment(amount);

        public void UpdateDescription(string description) => _task.Description = description;
    }

    private sealed class TextOnlyProgressContext : IProgressContext
    {
        private readonly IAnsiConsole _console;

        public TextOnlyProgressContext(IAnsiConsole console) => _console = console;

        public void UpdateStatus(string status) => _console.WriteLine($"  → {status}");
    }

    private sealed class TextOnlyProgressBarContext : IProgressBarContext
    {
        private readonly IAnsiConsole _console;
        private readonly string _description;
        private readonly int _total;
        private int _current;

        public TextOnlyProgressBarContext(IAnsiConsole console, string description, int total)
        {
            _console = console;
            _description = description;
            _total = total;
            _current = 0;
        }

        public void Increment(int amount = 1)
        {
            _current += amount;
            // Only show every 10% or on completion to avoid too much output
            var percent = (_current * 100) / _total;
            var prevPercent = ((_current - amount) * 100) / _total;
            if (percent / 10 > prevPercent / 10 || _current == _total)
            {
                _console.WriteLine($"  → {_description}: {_current}/{_total} ({percent}%)");
            }
        }

        public void UpdateDescription(string description)
        {
            // In text mode, we just note the description change
        }
    }
}
