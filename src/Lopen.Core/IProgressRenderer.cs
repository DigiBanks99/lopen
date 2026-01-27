namespace Lopen.Core;

/// <summary>
/// Context for updating progress status during an operation.
/// </summary>
public interface IProgressContext
{
    /// <summary>
    /// Update the status text displayed to the user.
    /// </summary>
    void UpdateStatus(string status);
}

/// <summary>
/// Context for updating a progress bar during batch operations.
/// </summary>
public interface IProgressBarContext
{
    /// <summary>
    /// Increment progress by the specified amount (default: 1).
    /// </summary>
    void Increment(int amount = 1);

    /// <summary>
    /// Update the description text for the current task.
    /// </summary>
    void UpdateDescription(string description);
}

/// <summary>
/// Renderer for progress indicators and spinners.
/// </summary>
public interface IProgressRenderer
{
    /// <summary>
    /// Show a spinner while executing an async operation that returns a value.
    /// </summary>
    Task<T> ShowProgressAsync<T>(
        string status,
        Func<IProgressContext, Task<T>> operation,
        CancellationToken ct = default);

    /// <summary>
    /// Show a spinner while executing an async operation.
    /// </summary>
    Task ShowProgressAsync(
        string status,
        Func<IProgressContext, Task> operation,
        CancellationToken ct = default);

    /// <summary>
    /// Show a progress bar for batch operations with known total count.
    /// </summary>
    /// <param name="description">Description of the operation (e.g., "Running tests").</param>
    /// <param name="totalCount">Total number of items to process.</param>
    /// <param name="operation">Async operation that receives progress bar context.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ShowProgressBarAsync(
        string description,
        int totalCount,
        Func<IProgressBarContext, Task> operation,
        CancellationToken ct = default);
}

/// <summary>
/// Spinner types for progress indication.
/// </summary>
public enum SpinnerType
{
    /// <summary>Default: calm, professional dots animation.</summary>
    Dots,

    /// <summary>For heavy processing operations.</summary>
    Arc,

    /// <summary>For fast, brief operations.</summary>
    Line,

    /// <summary>For long-running network calls.</summary>
    SimpleDotsScrolling
}
