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
