using System.Diagnostics;

namespace Lopen.Core.Testing;

/// <summary>
/// Test case that executes a CLI command and validates its output.
/// </summary>
public sealed class CommandTestCase : ITestCase
{
    private readonly string[] _commandArgs;
    private readonly ITestValidator _validator;
    private readonly int? _expectedExitCode;
    
    /// <inheritdoc/>
    public string TestId { get; }
    
    /// <inheritdoc/>
    public string Description { get; }
    
    /// <inheritdoc/>
    public string Suite { get; }
    
    /// <summary>
    /// Creates a command test case.
    /// </summary>
    /// <param name="testId">Unique test identifier.</param>
    /// <param name="description">Test description.</param>
    /// <param name="suite">Test suite name.</param>
    /// <param name="commandArgs">Command arguments (after lopen).</param>
    /// <param name="validator">Validator for the command output.</param>
    /// <param name="expectedExitCode">Expected exit code (null to ignore).</param>
    public CommandTestCase(
        string testId,
        string description,
        string suite,
        string[] commandArgs,
        ITestValidator validator,
        int? expectedExitCode = null)
    {
        TestId = testId;
        Description = description;
        Suite = suite;
        _commandArgs = commandArgs;
        _validator = validator;
        _expectedExitCode = expectedExitCode;
    }
    
    /// <inheritdoc/>
    public async Task<TestResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(context.Timeout);
            
            var (output, exitCode) = await RunCommandAsync(context.LopenPath, cts.Token);
            
            stopwatch.Stop();
            
            // Check exit code if expected
            if (_expectedExitCode.HasValue && exitCode != _expectedExitCode.Value)
            {
                return CreateResult(startTime, stopwatch.Elapsed, TestStatus.Fail,
                    error: $"Expected exit code {_expectedExitCode.Value}, got {exitCode}",
                    responsePreview: TruncateResponse(output));
            }
            
            // Validate output
            var validation = _validator.Validate(output);
            
            if (validation.IsValid)
            {
                return CreateResult(startTime, stopwatch.Elapsed, TestStatus.Pass,
                    matchedPattern: validation.MatchedPattern,
                    responsePreview: TruncateResponse(output));
            }
            else
            {
                return CreateResult(startTime, stopwatch.Elapsed, TestStatus.Fail,
                    error: "No expected patterns found in response",
                    responsePreview: TruncateResponse(output));
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return CreateResult(startTime, stopwatch.Elapsed, TestStatus.Timeout,
                error: $"Test exceeded timeout of {context.Timeout.TotalSeconds}s");
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return CreateResult(startTime, stopwatch.Elapsed, TestStatus.Skipped,
                error: "Test cancelled");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return CreateResult(startTime, stopwatch.Elapsed, TestStatus.Error,
                error: ex.Message);
        }
    }
    
    private async Task<(string Output, int ExitCode)> RunCommandAsync(string lopenPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = lopenPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        foreach (var arg in _commandArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }
        
        using var process = new Process { StartInfo = startInfo };
        
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();
        
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };
        
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };
        
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        await process.WaitForExitAsync(cancellationToken);
        
        // Combine stdout and stderr
        var combinedOutput = outputBuilder.ToString();
        if (errorBuilder.Length > 0)
        {
            combinedOutput += "\n" + errorBuilder.ToString();
        }
        
        return (combinedOutput.Trim(), process.ExitCode);
    }
    
    private TestResult CreateResult(
        DateTimeOffset startTime,
        TimeSpan duration,
        TestStatus status,
        string? error = null,
        string? matchedPattern = null,
        string? responsePreview = null)
    {
        return new TestResult
        {
            TestId = TestId,
            Suite = Suite,
            Description = Description,
            Status = status,
            Duration = duration,
            StartTime = startTime,
            EndTime = startTime + duration,
            Error = error,
            MatchedPattern = matchedPattern,
            ResponsePreview = responsePreview,
            Input = string.Join(" ", _commandArgs)
        };
    }
    
    private static string TruncateResponse(string response, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(response))
            return string.Empty;
        
        return response.Length <= maxLength
            ? response
            : response[..maxLength] + "...";
    }
}
