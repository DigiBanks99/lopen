using Lopen.Core.Workflow;
using Microsoft.Extensions.Logging;
using Spectre.Tui;

namespace Lopen.Tui;

/// <summary>
/// Real TUI application shell using Spectre.Tui for full-screen cell-based rendering.
/// Manages the render loop, keyboard input, layout calculation, and component rendering.
/// </summary>
internal sealed class TuiApplication : ITuiApplication
{
    private readonly TopPanelComponent _topPanel;
    private readonly ActivityPanelComponent _activityPanel;
    private readonly ContextPanelComponent _contextPanel;
    private readonly PromptAreaComponent _promptArea;
    private readonly KeyboardHandler _keyboardHandler;
    private readonly ITopPanelDataProvider? _topPanelDataProvider;
    private readonly IContextPanelDataProvider? _contextPanelDataProvider;
    private readonly IActivityPanelDataProvider? _activityPanelDataProvider;
    private readonly ISlashCommandExecutor? _slashCommandExecutor;
    private readonly IPauseController? _pauseController;
    private readonly ILogger<TuiApplication> _logger;

    private volatile bool _running;
    private CancellationTokenSource? _stopCts;

    // Mutable state — updated by keyboard input and external events
    private FocusPanel _focus = FocusPanel.Prompt;
    private bool _isPaused;
    private int _splitPercent = 60;

    // Data bags for each panel (updated externally or by keyboard events)
    private TopPanelData _topData = new() { Version = "0.0.0" };
    private ActivityPanelData _activityData = new();
    private ContextPanelData _contextData = new();
    private PromptAreaData _promptData = new();

    // Throttle for data provider refresh (avoid calling async services every frame)
    private DateTime _lastProviderRefresh = DateTime.MinValue;
    private DateTime _lastContextProviderRefresh = DateTime.MinValue;
    internal static readonly TimeSpan ProviderRefreshInterval = TimeSpan.FromSeconds(1);

    public bool IsRunning => _running;

    // Allow test injection of the terminal factory
    internal Func<ITerminal>? TerminalFactory { get; set; }

    public TuiApplication(
        TopPanelComponent topPanel,
        ActivityPanelComponent activityPanel,
        ContextPanelComponent contextPanel,
        PromptAreaComponent promptArea,
        KeyboardHandler keyboardHandler,
        ILogger<TuiApplication> logger,
        ITopPanelDataProvider? topPanelDataProvider = null,
        IContextPanelDataProvider? contextPanelDataProvider = null,
        IActivityPanelDataProvider? activityPanelDataProvider = null,
        ISlashCommandExecutor? slashCommandExecutor = null,
        IPauseController? pauseController = null)
    {
        _topPanel = topPanel ?? throw new ArgumentNullException(nameof(topPanel));
        _activityPanel = activityPanel ?? throw new ArgumentNullException(nameof(activityPanel));
        _contextPanel = contextPanel ?? throw new ArgumentNullException(nameof(contextPanel));
        _promptArea = promptArea ?? throw new ArgumentNullException(nameof(promptArea));
        _keyboardHandler = keyboardHandler ?? throw new ArgumentNullException(nameof(keyboardHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _topPanelDataProvider = topPanelDataProvider;
        _contextPanelDataProvider = contextPanelDataProvider;
        _activityPanelDataProvider = activityPanelDataProvider;
        _slashCommandExecutor = slashCommandExecutor;
        _pauseController = pauseController;
    }

    public async Task RunAsync(string? initialPrompt = null, CancellationToken cancellationToken = default)
    {
        if (_running)
        {
            _logger.LogWarning("TUI application is already running");
            return;
        }

        _running = true;
        _stopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ct = _stopCts.Token;

        _logger.LogInformation("Starting TUI application");

        ITerminal? terminal = null;
        try
        {
            terminal = TerminalFactory?.Invoke()
                ?? Terminal.Create(new FullscreenMode());
            var renderer = new Renderer(terminal);
            renderer.SetTargetFps(30);

            while (!ct.IsCancellationRequested)
            {
                // 1. Poll keyboard input
                DrainKeyboardInput();

                // 2. Refresh top panel data from provider (throttled)
                await RefreshTopPanelDataAsync(ct).ConfigureAwait(false);

                // 2b. Refresh context panel data from provider (throttled)
                await RefreshContextPanelDataAsync(ct).ConfigureAwait(false);

                // 2c. Refresh activity panel data from provider
                RefreshActivityPanelData();

                // 3. Render frame
                renderer.Draw((ctx, _) => RenderFrame(ctx));

                // 4. Yield to avoid busy-wait
                await Task.Delay(1, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TUI application error");
        }
        finally
        {
            if (terminal is IDisposable disposable)
                disposable.Dispose();
            _running = false;
            _logger.LogInformation("TUI application stopped");
        }
    }

    public Task StopAsync()
    {
        _logger.LogInformation("Stopping TUI application");
        _stopCts?.Cancel();
        _running = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates the top panel data for the next render frame.
    /// </summary>
    public void UpdateTopPanel(TopPanelData data) => _topData = data;

    private async Task RefreshTopPanelDataAsync(CancellationToken cancellationToken)
    {
        if (_topPanelDataProvider is null)
            return;

        var now = DateTime.UtcNow;
        if (now - _lastProviderRefresh >= ProviderRefreshInterval)
        {
            _lastProviderRefresh = now;
            try
            {
                await _topPanelDataProvider.RefreshAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Failed to refresh top panel async data");
            }
        }

        // Always read synchronous data (tokens, workflow) fresh each frame
        _topData = _topPanelDataProvider.GetCurrentData();
    }

    private async Task RefreshContextPanelDataAsync(CancellationToken cancellationToken)
    {
        if (_contextPanelDataProvider is null)
            return;

        var now = DateTime.UtcNow;
        if (now - _lastContextProviderRefresh >= ProviderRefreshInterval)
        {
            _lastContextProviderRefresh = now;
            try
            {
                await _contextPanelDataProvider.RefreshAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Failed to refresh context panel async data");
            }
        }

        _contextData = _contextPanelDataProvider.GetCurrentData();
    }

    private void RefreshActivityPanelData()
    {
        if (_activityPanelDataProvider is not null)
            _activityData = _activityPanelDataProvider.GetCurrentData();
    }

    /// <summary>
    /// Updates the activity panel data for the next render frame.
    /// </summary>
    public void UpdateActivityPanel(ActivityPanelData data) => _activityData = data;

    /// <summary>
    /// Updates the context panel data for the next render frame.
    /// </summary>
    public void UpdateContextPanel(ContextPanelData data) => _contextData = data;

    /// <summary>
    /// Updates the prompt area data for the next render frame.
    /// </summary>
    public void UpdatePromptArea(PromptAreaData data) => _promptData = data;

    private void DrainKeyboardInput()
    {
        while (Console.KeyAvailable)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            var input = new KeyInput
            {
                Key = keyInfo.Key,
                Modifiers = keyInfo.Modifiers,
                KeyChar = keyInfo.KeyChar
            };
            var action = _keyboardHandler.Handle(input, _focus);
            ApplyAction(action, keyInfo);
        }
    }

    private void ApplyAction(KeyAction action, ConsoleKeyInfo keyInfo)
    {
        switch (action)
        {
            case KeyAction.CycleFocusForward:
                _focus = KeyboardHandler.CycleFocus(_focus);
                break;

            case KeyAction.TogglePause:
                _isPaused = !_isPaused;
                _pauseController?.Toggle();
                _promptData = _promptData with { IsPaused = _isPaused };
                break;

            case KeyAction.Cancel:
                _stopCts?.Cancel();
                break;

            case KeyAction.InsertNewline:
                _promptData = _promptData with
                {
                    Text = _promptData.Text + Environment.NewLine,
                    CursorPosition = _promptData.CursorPosition + Environment.NewLine.Length
                };
                break;

            case KeyAction.SubmitPrompt:
                var submittedText = _promptData.Text;
                _promptData = _promptData with { Text = string.Empty, CursorPosition = 0 };
                if (submittedText.StartsWith('/'))
                    _ = ProcessSlashCommandAsync(submittedText);
                // TODO: Wire non-slash input to orchestrator input queue
                break;

            case KeyAction.None when !char.IsControl(keyInfo.KeyChar) && keyInfo.KeyChar != '\0':
                _promptData = _promptData with
                {
                    Text = _promptData.Text + keyInfo.KeyChar,
                    CursorPosition = _promptData.CursorPosition + 1
                };
                break;
        }
    }

    internal void RenderFrame(RenderContext ctx)
    {
        var viewport = ctx.Viewport;
        var regions = LayoutCalculator.Calculate(
            viewport.Width, viewport.Height, _splitPercent);

        RenderRegion(ctx, regions.Header,
            _topPanel.Render(_topData, regions.Header));
        RenderRegion(ctx, regions.Activity,
            _activityPanel.Render(_activityData, regions.Activity));
        RenderRegion(ctx, regions.Context,
            _contextPanel.Render(_contextData, regions.Context));
        RenderRegion(ctx, regions.Prompt,
            _promptArea.Render(_promptData, regions.Prompt));
    }

    private async Task ProcessSlashCommandAsync(string input)
    {
        if (_slashCommandExecutor is null)
        {
            _logger.LogDebug("No slash command executor available");
            return;
        }

        try
        {
            var result = await _slashCommandExecutor.ExecuteAsync(input, _stopCts?.Token ?? CancellationToken.None)
                .ConfigureAwait(false);

            var kind = result.IsSuccess ? ActivityEntryKind.Command : ActivityEntryKind.Error;
            var summary = result.IsSuccess
                ? result.OutputMessage ?? $"Executed {result.Command}"
                : result.ErrorMessage ?? $"Failed: {result.Command}";

            var entry = new ActivityEntry
            {
                Summary = summary,
                Kind = kind
            };

            if (_activityPanelDataProvider is not null)
            {
                _activityPanelDataProvider.AddEntry(entry);
            }
            else
            {
                var entries = _activityData.Entries.Append(entry).ToList();
                _activityData = new ActivityPanelData { Entries = entries, ScrollOffset = _activityData.ScrollOffset };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error processing slash command: {Input}", input);
        }
    }

    private static void RenderRegion(RenderContext ctx, ScreenRect region, string[] lines)
    {
        for (var row = 0; row < region.Height; row++)
        {
            var line = row < lines.Length ? lines[row] : string.Empty;
            ctx.SetString(region.X, region.Y + row, line, null, region.Width);
        }
    }
}
