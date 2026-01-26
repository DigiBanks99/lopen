namespace Lopen.Core.Testing;

/// <summary>
/// Summary of a test run.
/// </summary>
public sealed record TestRunSummary
{
    /// <summary>When the test run started.</summary>
    public DateTimeOffset StartTime { get; init; }
    
    /// <summary>When the test run ended.</summary>
    public DateTimeOffset EndTime { get; init; }
    
    /// <summary>Total duration of the test run.</summary>
    public TimeSpan Duration => EndTime - StartTime;
    
    /// <summary>AI model used for tests.</summary>
    public string Model { get; init; } = string.Empty;
    
    /// <summary>Individual test results.</summary>
    public IReadOnlyList<TestResult> Results { get; init; } = [];
    
    /// <summary>Total number of tests.</summary>
    public int Total => Results.Count;
    
    /// <summary>Number of passed tests.</summary>
    public int Passed => Results.Count(r => r.Status == TestStatus.Pass);
    
    /// <summary>Number of failed tests.</summary>
    public int Failed => Results.Count(r => r.Status == TestStatus.Fail);
    
    /// <summary>Number of timed out tests.</summary>
    public int Timeouts => Results.Count(r => r.Status == TestStatus.Timeout);
    
    /// <summary>Number of tests with errors.</summary>
    public int Errors => Results.Count(r => r.Status == TestStatus.Error);
    
    /// <summary>Number of skipped tests.</summary>
    public int Skipped => Results.Count(r => r.Status == TestStatus.Skipped);
    
    /// <summary>Whether all tests passed.</summary>
    public bool AllPassed => Passed == Total && Total > 0;
    
    /// <summary>Success rate as a percentage.</summary>
    public double SuccessRate => Total > 0 ? (double)Passed / Total * 100 : 0;
}
