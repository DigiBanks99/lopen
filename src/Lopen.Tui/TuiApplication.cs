using Lopen.Core.Workflow;
using Microsoft.Extensions.Logging;
using Spectre.Tui;

namespace Lopen.Tui;

/// <summary>
/// Tracks which modal overlay (if any) is currently displayed.
/// </summary>
internal enum TuiModalState
{
    None,
    LandingPage,
    SessionResume,
    ResourceViewer,
    FilePicker
}

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
    private readonly IUserPromptQueue? _userPromptQueue;
    private readonly LandingPageComponent _landingPage;
    private readonly SessionResumeModalComponent _sessionResumeModal;
    private readonly ResourceViewerModalComponent _resourceViewerModal;
    private readonly FilePickerComponent _filePickerComponent;
    private readonly ISessionDetector? _sessionDetector;
    private readonly bool _showLandingPage;
    private readonly ILogger<TuiApplication> _logger;

    private volatile bool _running;
    private CancellationTokenSource? _stopCts;

    // Mutable state — updated by keyboard input and external events
    private FocusPanel _focus = FocusPanel.Prompt;
    private bool _isPaused;
    private int _splitPercent = 60;
    private TuiModalState _modalState = TuiModalState.None;

    // Data bags for each panel (updated externally or by keyboard events)
    private TopPanelData _topData = new() { Version = "0.0.0" };
    private ActivityPanelData _activityData = new();
    private ContextPanelData _contextData = new();
    private PromptAreaData _promptData = new();
    private LandingPageData _landingPageData = new() { Version = "0.0.0" };
    private SessionResumeData _sessionResumeData = new()
    {
        ModuleName = "", PhaseName = "", StepProgress = "", TaskProgress = "", LastActivity = ""
    };
    private ResourceViewerData _resourceViewerData = new() { Label = "" };
    private FilePickerData _filePickerData = new() { RootPath = "" };

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
        IPauseController? pauseController = null,
        IUserPromptQueue? userPromptQueue = null,
        bool showLandingPage = true,
        ISessionDetector? sessionDetector = null)
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
        _userPromptQueue = userPromptQueue;
        _landingPage = new LandingPageComponent();
        _sessionResumeModal = new SessionResumeModalComponent();
        _resourceViewerModal = new ResourceViewerModalComponent();
        _filePickerComponent = new FilePickerComponent();
        _sessionDetector = sessionDetector;
        _showLandingPage = showLandingPage;
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

        // Show landing page on startup (unless disabled via --no-welcome)
        if (_showLandingPage)
            _modalState = TuiModalState.LandingPage;
        else
            await CheckForActiveSessionAsync(ct).ConfigureAwait(false);

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

                // 2d. Refresh context-aware keyboard hints
                _promptData = _promptData with
                {
                    CustomHints = KeyboardHandler.GetHints(_focus, _isPaused)
                };

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

        // Keep landing page version in sync
        if (_modalState == TuiModalState.LandingPage)
        {
            _landingPageData = _landingPageData with
            {
                Version = _topData.Version,
                IsAuthenticated = _topData.IsAuthenticated
            };
        }
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

    /// <summary>
    /// Updates the context panel data for the next render frame.
    /// </summary>
    public void UpdateContextData(ContextPanelData data) => _contextData = data;

    /// <summary>
    /// Updates the landing page data for the modal overlay.
    /// </summary>
    public void UpdateLandingPage(LandingPageData data) => _landingPageData = data;

    /// <summary>
    /// Updates the session resume data for the modal overlay.
    /// </summary>
    public void UpdateSessionResume(SessionResumeData data) => _sessionResumeData = data;

    private void DrainKeyboardInput()
    {
        while (Console.KeyAvailable)
        {
            var keyInfo = Console.ReadKey(intercept: true);

            // Landing page: any keypress dismisses it, then check for session
            if (_modalState == TuiModalState.LandingPage)
            {
                _modalState = TuiModalState.None;
                _ = CheckForActiveSessionAsync(_stopCts?.Token ?? CancellationToken.None);
                continue;
            }

            // Session resume modal: arrow keys navigate, enter confirms
            if (_modalState == TuiModalState.SessionResume)
            {
                HandleSessionResumeInput(keyInfo);
                continue;
            }

            // Resource viewer modal: Esc closes, Up/Down scrolls
            if (_modalState == TuiModalState.ResourceViewer)
            {
                HandleResourceViewerInput(keyInfo);
                continue;
            }

            // File picker modal: Esc closes, Up/Down navigates, Enter expands/collapses
            if (_modalState == TuiModalState.FilePicker)
            {
                HandleFilePickerInput(keyInfo);
                continue;
            }

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

            case KeyAction.Backspace:
                if (_promptData.Text.Length > 0)
                {
                    _promptData = _promptData with
                    {
                        Text = _promptData.Text[..^1],
                        CursorPosition = Math.Max(0, _promptData.CursorPosition - 1)
                    };
                }
                break;

            case KeyAction.SubmitPrompt:
                var submittedText = _promptData.Text;
                _promptData = _promptData with { Text = string.Empty, CursorPosition = 0 };
                if (submittedText.StartsWith('/'))
                    _ = ProcessSlashCommandAsync(submittedText);
                else if (!string.IsNullOrWhiteSpace(submittedText))
                    _userPromptQueue?.Enqueue(submittedText);
                break;

            case KeyAction.None when !char.IsControl(keyInfo.KeyChar) && keyInfo.KeyChar != '\0':
                _promptData = _promptData with
                {
                    Text = _promptData.Text + keyInfo.KeyChar,
                    CursorPosition = _promptData.CursorPosition + 1
                };
                break;

            case KeyAction.ToggleExpand:
                ToggleSelectedEntry();
                break;

            case KeyAction.ScrollUp:
                if (_focus == FocusPanel.Activity && _activityData.Entries.Count > 0)
                {
                    var idx = _activityData.SelectedEntryIndex;
                    _activityData = _activityData with
                    {
                        SelectedEntryIndex = Math.Max(0, idx <= 0 ? _activityData.Entries.Count - 1 : idx - 1)
                    };
                }
                break;

            case KeyAction.ScrollDown:
                if (_focus == FocusPanel.Activity && _activityData.Entries.Count > 0)
                {
                    var idx2 = _activityData.SelectedEntryIndex;
                    _activityData = _activityData with
                    {
                        SelectedEntryIndex = idx2 >= _activityData.Entries.Count - 1 ? 0 : idx2 + 1
                    };
                }
                break;

            case KeyAction.ViewResource1:
            case KeyAction.ViewResource2:
            case KeyAction.ViewResource3:
            case KeyAction.ViewResource4:
            case KeyAction.ViewResource5:
            case KeyAction.ViewResource6:
            case KeyAction.ViewResource7:
            case KeyAction.ViewResource8:
            case KeyAction.ViewResource9:
                OpenResourceViewer(action - KeyAction.ViewResource1);
                break;
        }
    }

    internal void RenderFrame(RenderContext ctx)
    {
        var viewport = ctx.Viewport;

        // If a modal is active, render it fullscreen
        if (_modalState == TuiModalState.LandingPage)
        {
            var region = new ScreenRect(0, 0, viewport.Width, viewport.Height);
            RenderRegion(ctx, region, _landingPage.Render(_landingPageData, region));
            return;
        }

        if (_modalState == TuiModalState.SessionResume)
        {
            var region = new ScreenRect(0, 0, viewport.Width, viewport.Height);
            RenderRegion(ctx, region, _sessionResumeModal.Render(_sessionResumeData, region));
            return;
        }

        if (_modalState == TuiModalState.ResourceViewer)
        {
            var region = new ScreenRect(0, 0, viewport.Width, viewport.Height);
            RenderRegion(ctx, region, _resourceViewerModal.Render(_resourceViewerData, region));
            return;
        }

        if (_modalState == TuiModalState.FilePicker)
        {
            var region = new ScreenRect(0, 0, viewport.Width, viewport.Height);
            RenderRegion(ctx, region, _filePickerComponent.Render(_filePickerData, region));
            return;
        }

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

    private void HandleSessionResumeInput(ConsoleKeyInfo keyInfo)
    {
        const int optionCount = 3; // Resume, Start New, View Details

        switch (keyInfo.Key)
        {
            case ConsoleKey.LeftArrow:
                _sessionResumeData = _sessionResumeData with
                {
                    SelectedOption = Math.Max(0, _sessionResumeData.SelectedOption - 1)
                };
                break;

            case ConsoleKey.RightArrow:
                _sessionResumeData = _sessionResumeData with
                {
                    SelectedOption = Math.Min(optionCount - 1, _sessionResumeData.SelectedOption + 1)
                };
                break;

            case ConsoleKey.Enter:
                var selected = _sessionResumeData.SelectedOption;
                _modalState = TuiModalState.None;
                _logger.LogInformation("Session resume option selected: {Option}",
                    SessionResumeModalComponent.Options[selected]);
                break;

            case ConsoleKey.Escape:
                _modalState = TuiModalState.None;
                break;
        }
    }

    private void ToggleSelectedEntry()
    {
        if (_focus != FocusPanel.Activity || _activityData.Entries.Count == 0)
            return;

        var idx = _activityData.SelectedEntryIndex;
        if (idx < 0 || idx >= _activityData.Entries.Count)
            return;

        var entry = _activityData.Entries[idx];

        // If already expanded and has full document, drill into it
        if (entry.IsExpanded && entry.FullDocumentContent is not null)
        {
            _resourceViewerData = new ResourceViewerData
            {
                Label = entry.Summary,
                Lines = entry.FullDocumentContent.Split('\n').ToList(),
                ScrollOffset = 0
            };
            _modalState = TuiModalState.ResourceViewer;
            return;
        }

        var entries = _activityData.Entries.ToList();
        entries[idx] = entry with { IsExpanded = !entry.IsExpanded };
        _activityData = _activityData with { Entries = entries };
    }

    internal void OpenResourceViewer(int resourceIndex)
    {
        if (resourceIndex < 0 || resourceIndex >= _contextData.Resources.Count)
            return;

        var resource = _contextData.Resources[resourceIndex];
        var contentLines = (resource.Content ?? "(No content available)")
            .Split('\n')
            .ToList();

        _resourceViewerData = new ResourceViewerData
        {
            Label = resource.Label,
            Lines = contentLines,
            ScrollOffset = 0
        };
        _modalState = TuiModalState.ResourceViewer;
    }

    private void HandleResourceViewerInput(ConsoleKeyInfo keyInfo)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.Escape:
                _modalState = TuiModalState.None;
                break;
            case ConsoleKey.UpArrow:
                _resourceViewerData = _resourceViewerData with
                {
                    ScrollOffset = Math.Max(0, _resourceViewerData.ScrollOffset - 1)
                };
                break;
            case ConsoleKey.DownArrow:
                _resourceViewerData = _resourceViewerData with
                {
                    ScrollOffset = _resourceViewerData.ScrollOffset + 1
                };
                break;
        }
    }

    /// <summary>
    /// Opens the file picker modal with the specified data.
    /// </summary>
    internal void OpenFilePicker(FilePickerData data)
    {
        _filePickerData = data;
        _modalState = TuiModalState.FilePicker;
    }

    private void HandleFilePickerInput(ConsoleKeyInfo keyInfo)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.Escape:
                _modalState = TuiModalState.None;
                break;
            case ConsoleKey.UpArrow:
                if (_filePickerData.Nodes.Count > 0)
                {
                    _filePickerData = _filePickerData with
                    {
                        SelectedIndex = Math.Max(0, _filePickerData.SelectedIndex - 1)
                    };
                }
                break;
            case ConsoleKey.DownArrow:
                if (_filePickerData.Nodes.Count > 0)
                {
                    _filePickerData = _filePickerData with
                    {
                        SelectedIndex = Math.Min(_filePickerData.Nodes.Count - 1, _filePickerData.SelectedIndex + 1)
                    };
                }
                break;
            case ConsoleKey.Enter:
                ToggleFilePickerNode();
                break;
        }
    }

    private void ToggleFilePickerNode()
    {
        var idx = _filePickerData.SelectedIndex;
        if (idx < 0 || idx >= _filePickerData.Nodes.Count)
            return;

        var node = _filePickerData.Nodes[idx];
        if (!node.IsDirectory)
            return;

        var nodes = _filePickerData.Nodes.ToList();
        nodes[idx] = node with { IsExpanded = !node.IsExpanded };
        _filePickerData = _filePickerData with { Nodes = nodes };
    }

    private async Task CheckForActiveSessionAsync(CancellationToken cancellationToken)
    {
        if (_sessionDetector is null)
            return;

        try
        {
            var resumeData = await _sessionDetector.DetectActiveSessionAsync(cancellationToken)
                .ConfigureAwait(false);

            if (resumeData is not null)
            {
                _sessionResumeData = resumeData;
                _modalState = TuiModalState.SessionResume;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Failed to detect active session");
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
