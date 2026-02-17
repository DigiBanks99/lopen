using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Tui.Tests;

public class FailureModalWiringTests
{
    #region Stubs

    private sealed class StubActivityPanelDataProvider : IActivityPanelDataProvider
    {
        public int ConsecutiveFailureCount { get; set; }
        public ActivityPanelData GetCurrentData() => new();
        public void AddEntry(ActivityEntry entry) { }
        public void Clear() { }
        public void AddPhaseTransition(string fromPhase, string toPhase, IReadOnlyList<string>? sections = null) { }
        public void AddFileEdit(string filePath, int linesAdded, int linesRemoved, IReadOnlyList<string>? diffLines = null) { }
        public void AddTaskFailure(string taskName, string errorMessage, IReadOnlyList<string>? details = null) { }
    }

    private sealed class StubUserPromptQueue : Core.IUserPromptQueue
    {
        public List<string> EnqueuedPrompts { get; } = [];
        public void Enqueue(string prompt) => EnqueuedPrompts.Add(prompt);
        public bool TryDequeue(out string prompt) { prompt = ""; return false; }
        public Task<string> DequeueAsync(CancellationToken cancellationToken = default) => Task.FromResult("");
        public int Count => EnqueuedPrompts.Count;
    }

    #endregion

    private static TuiApplication CreateApp(
        IActivityPanelDataProvider? activityProvider = null,
        Core.IUserPromptQueue? userPromptQueue = null,
        int failureThreshold = 3)
    {
        return new TuiApplication(
            new TopPanelComponent(),
            new ActivityPanelComponent(),
            new ContextPanelComponent(),
            new PromptAreaComponent(),
            new KeyboardHandler(),
            NullLogger<TuiApplication>.Instance,
            activityPanelDataProvider: activityProvider,
            userPromptQueue: userPromptQueue,
            showLandingPage: false,
            failureThreshold: failureThreshold);
    }

    [Fact]
    public void RefreshActivityPanelData_BelowThreshold_DoesNotShowModal()
    {
        var provider = new StubActivityPanelDataProvider { ConsecutiveFailureCount = 2 };
        var app = CreateApp(activityProvider: provider);

        app.RefreshActivityPanelData();

        Assert.Equal(TuiModalState.None, app.CurrentModalState);
    }

    [Fact]
    public void RefreshActivityPanelData_AtThreshold_ShowsErrorModal()
    {
        var provider = new StubActivityPanelDataProvider { ConsecutiveFailureCount = 3 };
        var app = CreateApp(activityProvider: provider);

        app.RefreshActivityPanelData();

        Assert.Equal(TuiModalState.ErrorModal, app.CurrentModalState);
    }

    [Fact]
    public void RefreshActivityPanelData_AboveThreshold_ShowsErrorModal()
    {
        var provider = new StubActivityPanelDataProvider { ConsecutiveFailureCount = 5 };
        var app = CreateApp(activityProvider: provider);

        app.RefreshActivityPanelData();

        Assert.Equal(TuiModalState.ErrorModal, app.CurrentModalState);
    }

    [Fact]
    public void RefreshActivityPanelData_OncePerStreak_DoesNotShowDuplicateModal()
    {
        var provider = new StubActivityPanelDataProvider { ConsecutiveFailureCount = 3 };
        var app = CreateApp(activityProvider: provider);

        // First call triggers the modal
        app.RefreshActivityPanelData();
        Assert.Equal(TuiModalState.ErrorModal, app.CurrentModalState);

        // Dismiss the modal to simulate user closing it
        app.DismissModal();
        Assert.Equal(TuiModalState.None, app.CurrentModalState);

        // Second call should not re-show (guard prevents duplicate in same streak)
        app.RefreshActivityPanelData();
        Assert.Equal(TuiModalState.None, app.CurrentModalState);
    }

    [Fact]
    public void RefreshActivityPanelData_StreakReset_AllowsNewModal()
    {
        var provider = new StubActivityPanelDataProvider { ConsecutiveFailureCount = 3 };
        var app = CreateApp(activityProvider: provider);

        // First streak triggers modal
        app.RefreshActivityPanelData();
        Assert.Equal(TuiModalState.ErrorModal, app.CurrentModalState);

        // Dismiss and break the streak
        app.DismissModal();
        provider.ConsecutiveFailureCount = 0;
        app.RefreshActivityPanelData();

        // New streak triggers modal again
        provider.ConsecutiveFailureCount = 3;
        app.RefreshActivityPanelData();
        Assert.Equal(TuiModalState.ErrorModal, app.CurrentModalState);
    }

    [Fact]
    public void RefreshActivityPanelData_ExistingModal_DoesNotOverride()
    {
        var provider = new StubActivityPanelDataProvider { ConsecutiveFailureCount = 3 };
        var app = CreateApp(activityProvider: provider);

        // Open a confirmation modal first
        app.OpenConfirmation(new ConfirmationData { Title = "Test", Message = "Confirm?" });
        Assert.Equal(TuiModalState.Confirmation, app.CurrentModalState);

        // Refresh should not override the existing modal
        app.RefreshActivityPanelData();
        Assert.Equal(TuiModalState.Confirmation, app.CurrentModalState);
    }

    [Fact]
    public void RefreshActivityPanelData_CustomThreshold_Respected()
    {
        var provider = new StubActivityPanelDataProvider { ConsecutiveFailureCount = 3 };
        var app = CreateApp(activityProvider: provider, failureThreshold: 5);

        // Below custom threshold: no modal
        app.RefreshActivityPanelData();
        Assert.Equal(TuiModalState.None, app.CurrentModalState);

        // At custom threshold: modal shown
        provider.ConsecutiveFailureCount = 5;
        app.RefreshActivityPanelData();
        Assert.Equal(TuiModalState.ErrorModal, app.CurrentModalState);
    }

    [Fact]
    public void RefreshActivityPanelData_NullProvider_DoesNotThrow()
    {
        var app = CreateApp(activityProvider: null);

        var ex = Record.Exception(() => app.RefreshActivityPanelData());

        Assert.Null(ex);
        Assert.Equal(TuiModalState.None, app.CurrentModalState);
    }

    [Fact]
    public void ErrorModalData_OnSelected_CallbackIsStored()
    {
        int? selected = null;
        var data = new ErrorModalData
        {
            Title = "Test",
            Message = "Error",
            OnSelected = i => selected = i
        };

        data.OnSelected?.Invoke(1);

        Assert.Equal(1, selected);
    }

    [Fact]
    public void RefreshActivityPanelData_ModalOpened_HasCorrectMessageAndCallback()
    {
        var provider = new StubActivityPanelDataProvider { ConsecutiveFailureCount = 4 };
        var app = CreateApp(activityProvider: provider);

        app.RefreshActivityPanelData();

        Assert.Equal(TuiModalState.ErrorModal, app.CurrentModalState);
        Assert.Equal("Repeated Failures Detected", app.CurrentErrorModalData.Title);
        Assert.Contains("4 consecutive task failures", app.CurrentErrorModalData.Message);
        Assert.NotNull(app.CurrentErrorModalData.OnSelected);
    }

    [Fact]
    public void HandleFailureModalSelection_EnqueuesIntervention()
    {
        var provider = new StubActivityPanelDataProvider { ConsecutiveFailureCount = 3 };
        var queue = new StubUserPromptQueue();
        var app = CreateApp(activityProvider: provider, userPromptQueue: queue);

        // Trigger modal so OnSelected is wired to HandleFailureModalSelection
        app.RefreshActivityPanelData();
        Assert.Equal(TuiModalState.ErrorModal, app.CurrentModalState);

        // Invoke the callback directly with index 0 (Retry)
        app.CurrentErrorModalData.OnSelected?.Invoke(0);

        Assert.Single(queue.EnqueuedPrompts);
        Assert.Equal("[intervention:retry]", queue.EnqueuedPrompts[0]);
    }

    [Fact]
    public void HandleFailureModalSelection_Skip_EnqueuesCorrectIntervention()
    {
        var provider = new StubActivityPanelDataProvider { ConsecutiveFailureCount = 3 };
        var queue = new StubUserPromptQueue();
        var app = CreateApp(activityProvider: provider, userPromptQueue: queue);

        app.RefreshActivityPanelData();
        app.CurrentErrorModalData.OnSelected?.Invoke(1); // Skip

        Assert.Single(queue.EnqueuedPrompts);
        Assert.Equal("[intervention:skip]", queue.EnqueuedPrompts[0]);
    }

    [Fact]
    public void HandleFailureModalSelection_Abort_EnqueuesCorrectIntervention()
    {
        var provider = new StubActivityPanelDataProvider { ConsecutiveFailureCount = 3 };
        var queue = new StubUserPromptQueue();
        var app = CreateApp(activityProvider: provider, userPromptQueue: queue);

        app.RefreshActivityPanelData();
        app.CurrentErrorModalData.OnSelected?.Invoke(2); // Abort

        Assert.Single(queue.EnqueuedPrompts);
        Assert.Equal("[intervention:abort]", queue.EnqueuedPrompts[0]);
    }
}
