using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lopen.Core.Testing;

/// <summary>
/// Renders test results to console output.
/// </summary>
public sealed class TestOutputService
{
    private readonly ConsoleOutput _output;
    
    /// <summary>
    /// Creates a test output service.
    /// </summary>
    public TestOutputService(ConsoleOutput output)
    {
        _output = output;
    }
    
    /// <summary>
    /// Display a header panel before tests run.
    /// </summary>
    public void DisplayHeader(string model, int testCount)
    {
        _output.Info($"Lopen Self-Test Suite");
        _output.KeyValue("Model", model);
        _output.KeyValue("Tests", testCount.ToString());
        _output.WriteLine();
    }
    
    /// <summary>
    /// Display test results as a table.
    /// </summary>
    public void DisplayResults(TestRunSummary summary)
    {
        var tableConfig = new TableConfig<TestResult>
        {
            Columns = new List<TableColumn<TestResult>>
            {
                new() { Header = "Test ID", Selector = r => r.TestId },
                new() { Header = "Status", Selector = r => FormatStatus(r.Status) },
                new() { Header = "Duration", Selector = r => $"{r.Duration.TotalSeconds:F1}s" },
                new() { Header = "Details", Selector = r => TruncateDetails(r) }
            },
            ShowRowCount = false
        };
        
        _output.Table(summary.Results.ToList(), tableConfig);
    }
    
    /// <summary>
    /// Display summary panel after tests complete.
    /// </summary>
    public void DisplaySummary(TestRunSummary summary)
    {
        _output.WriteLine();
        
        if (summary.AllPassed)
        {
            _output.Success($"All {summary.Total} tests passed");
        }
        else
        {
            _output.Error($"{summary.Failed + summary.Errors + summary.Timeouts} of {summary.Total} tests failed");
        }
        
        _output.KeyValue("Passed", $"{summary.Passed}/{summary.Total}");
        if (summary.Failed > 0)
            _output.KeyValue("Failed", summary.Failed.ToString());
        if (summary.Timeouts > 0)
            _output.KeyValue("Timeouts", summary.Timeouts.ToString());
        if (summary.Errors > 0)
            _output.KeyValue("Errors", summary.Errors.ToString());
        _output.KeyValue("Duration", $"{summary.Duration.TotalSeconds:F1}s");
    }
    
    /// <summary>
    /// Display verbose output for a single test result.
    /// </summary>
    public void DisplayVerboseResult(TestResult result)
    {
        var statusSymbol = result.Status switch
        {
            TestStatus.Pass => "✓",
            TestStatus.Fail => "✗",
            TestStatus.Timeout => "⏱",
            TestStatus.Error => "⚠",
            _ => "-"
        };
        
        // Format timestamp as HH:mm:ss.fff for brevity
        var timestamp = result.StartTime != default 
            ? result.StartTime.ToString("HH:mm:ss.fff") 
            : "";
        
        var prefix = string.IsNullOrEmpty(timestamp) ? "" : $"[{timestamp}] ";
        
        _output.WriteLine($"{prefix}{statusSymbol} {result.TestId}: {result.Description} ({result.Duration.TotalSeconds:F1}s)");
        
        if (result.Status != TestStatus.Pass && result.Error is not null)
        {
            _output.Muted($"  Error: {result.Error}");
        }
        
        if (result.ResponsePreview is not null && result.Status != TestStatus.Pass)
        {
            _output.Muted($"  Response: {result.ResponsePreview}");
        }
    }
    
    /// <summary>
    /// Output results as JSON.
    /// </summary>
    public string FormatAsJson(TestRunSummary summary)
    {
        var output = new TestRunJsonOutput
        {
            Summary = new TestSummaryJson
            {
                Total = summary.Total,
                Passed = summary.Passed,
                Failed = summary.Failed,
                Timeouts = summary.Timeouts,
                Errors = summary.Errors,
                DurationSeconds = summary.Duration.TotalSeconds,
                Model = summary.Model
            },
            Results = summary.Results.Select(r => new TestResultJson
            {
                TestId = r.TestId,
                Suite = r.Suite,
                Description = r.Description,
                Status = r.Status.ToString().ToLowerInvariant(),
                DurationSeconds = r.Duration.TotalSeconds,
                StartTime = r.StartTime != default ? r.StartTime : null,
                EndTime = r.EndTime != default ? r.EndTime : null,
                Input = r.Input,
                ResponsePreview = r.ResponsePreview,
                MatchedPattern = r.MatchedPattern,
                Error = r.Error
            }).ToList()
        };
        
        return JsonSerializer.Serialize(output, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }
    
    private static string FormatStatus(TestStatus status) => status switch
    {
        TestStatus.Pass => "✓ PASS",
        TestStatus.Fail => "✗ FAIL",
        TestStatus.Timeout => "⏱ TIMEOUT",
        TestStatus.Error => "⚠ ERROR",
        TestStatus.Skipped => "- SKIP",
        _ => "?"
    };
    
    private static string TruncateDetails(TestResult result)
    {
        var detail = result.Status == TestStatus.Pass
            ? result.MatchedPattern ?? ""
            : result.Error ?? "";
        
        return detail.Length > 40
            ? detail[..37] + "..."
            : detail;
    }
}

/// <summary>
/// JSON output structure for test run.
/// </summary>
internal sealed class TestRunJsonOutput
{
    public required TestSummaryJson Summary { get; init; }
    public required List<TestResultJson> Results { get; init; }
}

/// <summary>
/// JSON structure for test summary.
/// </summary>
internal sealed class TestSummaryJson
{
    public int Total { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public int Timeouts { get; init; }
    public int Errors { get; init; }
    public double DurationSeconds { get; init; }
    public string Model { get; init; } = string.Empty;
}

/// <summary>
/// JSON structure for individual test result.
/// </summary>
internal sealed class TestResultJson
{
    public string TestId { get; init; } = string.Empty;
    public string Suite { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public double DurationSeconds { get; init; }
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public string? Input { get; init; }
    public string? ResponsePreview { get; init; }
    public string? MatchedPattern { get; init; }
    public string? Error { get; init; }
}
