namespace Lopen.Core;

/// <summary>
/// Mock implementation of IProgressRenderer for testing.
/// Records all status updates for verification.
/// </summary>
public class MockProgressRenderer : IProgressRenderer
{
    private readonly List<string> _statusUpdates = new();
    private readonly List<ProgressBarRecord> _progressBarCalls = new();

    /// <summary>
    /// All status messages shown during operations.
    /// </summary>
    public IReadOnlyList<string> StatusUpdates => _statusUpdates;

    /// <summary>
    /// Number of times ShowProgressAsync was called.
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// Number of times ShowProgressBarAsync was called.
    /// </summary>
    public int ProgressBarCallCount { get; private set; }

    /// <summary>
    /// Records of progress bar calls.
    /// </summary>
    public IReadOnlyList<ProgressBarRecord> ProgressBarCalls => _progressBarCalls;

    /// <summary>
    /// Whether the operation was executed.
    /// </summary>
    public bool OperationExecuted { get; private set; }

    /// <summary>
    /// Exception to throw when operation is executed, for error testing.
    /// </summary>
    public Exception? ExceptionToThrow { get; set; }

    public async Task<T> ShowProgressAsync<T>(
        string status,
        Func<IProgressContext, Task<T>> operation,
        CancellationToken ct = default)
    {
        CallCount++;
        _statusUpdates.Add(status);

        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        var context = new MockProgressContext(_statusUpdates);
        OperationExecuted = true;
        return await operation(context);
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
        ProgressBarCallCount++;
        var record = new ProgressBarRecord(description, totalCount);
        _progressBarCalls.Add(record);
        
        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        var context = new MockProgressBarContext(record);
        OperationExecuted = true;
        await operation(context);
    }

    /// <summary>
    /// Reset the mock state.
    /// </summary>
    public void Reset()
    {
        _statusUpdates.Clear();
        _progressBarCalls.Clear();
        CallCount = 0;
        ProgressBarCallCount = 0;
        OperationExecuted = false;
        ExceptionToThrow = null;
    }

    private sealed class MockProgressContext : IProgressContext
    {
        private readonly List<string> _updates;

        public MockProgressContext(List<string> updates) => _updates = updates;

        public void UpdateStatus(string status) => _updates.Add(status);
    }

    private sealed class MockProgressBarContext : IProgressBarContext
    {
        private readonly ProgressBarRecord _record;

        public MockProgressBarContext(ProgressBarRecord record) => _record = record;

        public void Increment(int amount = 1) => _record.RecordIncrement(amount);

        public void UpdateDescription(string description) => _record.RecordDescription(description);
    }
}

/// <summary>
/// Record of a progress bar operation for testing.
/// </summary>
public sealed class ProgressBarRecord
{
    private readonly List<int> _increments = new();
    private readonly List<string> _descriptions = new();

    public ProgressBarRecord(string description, int totalCount)
    {
        Description = description;
        TotalCount = totalCount;
    }

    /// <summary>Initial description of the progress bar.</summary>
    public string Description { get; }

    /// <summary>Total count for the progress bar.</summary>
    public int TotalCount { get; }

    /// <summary>All increment amounts recorded.</summary>
    public IReadOnlyList<int> Increments => _increments;

    /// <summary>All description updates recorded.</summary>
    public IReadOnlyList<string> DescriptionUpdates => _descriptions;

    /// <summary>Current progress value (sum of increments).</summary>
    public int CurrentValue => _increments.Sum();

    internal void RecordIncrement(int amount) => _increments.Add(amount);
    internal void RecordDescription(string description) => _descriptions.Add(description);
}
