namespace Lopen.Core;

/// <summary>
/// Mock implementation of IProgressRenderer for testing.
/// Records all status updates for verification.
/// </summary>
public class MockProgressRenderer : IProgressRenderer
{
    private readonly List<string> _statusUpdates = new();

    /// <summary>
    /// All status messages shown during operations.
    /// </summary>
    public IReadOnlyList<string> StatusUpdates => _statusUpdates;

    /// <summary>
    /// Number of times ShowProgressAsync was called.
    /// </summary>
    public int CallCount { get; private set; }

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

    /// <summary>
    /// Reset the mock state.
    /// </summary>
    public void Reset()
    {
        _statusUpdates.Clear();
        CallCount = 0;
        OperationExecuted = false;
        ExceptionToThrow = null;
    }

    private sealed class MockProgressContext : IProgressContext
    {
        private readonly List<string> _updates;

        public MockProgressContext(List<string> updates) => _updates = updates;

        public void UpdateStatus(string status) => _updates.Add(status);
    }
}
