# Lopen Self-Testing Module - Comprehensive Research Document

**Target Framework:** C# .NET 9.0  
**Dependencies:** Spectre.Console 0.54.0, System.CommandLine 2.0.2  
**Document Date:** January 2025  
**Specification:** REQ-020 (Self-Testing Command)

---

## Table of Contents

- [Executive Summary](#executive-summary)
- [1. TestRunner Architecture](#1-testrunner-architecture)
  - [1.1 Recommended Design Pattern: Command + Strategy Pattern](#11-recommended-design-pattern-command--strategy-pattern)
  - [1.2 Core Architecture Components](#12-core-architecture-components)
  - [1.3 Concrete Test Case Implementation](#13-concrete-test-case-implementation)
  - [1.4 Dependency Injection Setup](#14-dependency-injection-setup)
  - [1.5 Architecture Decision Records (ADRs)](#15-architecture-decision-records-adrs)
- [2. Test Case Definition](#2-test-case-definition)
  - [2.1 Embedded vs External Test Definitions](#21-embedded-vs-external-test-definitions)
  - [2.2 Recommended Format: C# with JSON Support](#22-recommended-format-c-with-json-support)
  - [2.3 Test Case Schema (JSON)](#23-test-case-schema-json)
  - [2.4 Test Case Builder Pattern](#24-test-case-builder-pattern)
  - [2.5 Test Discovery & Loading](#25-test-discovery--loading)
- [3. AI Response Validation](#3-ai-response-validation)
  - [3.1 Validation Strategies](#31-validation-strategies)
  - [3.2 Recommended Strategy: Keyword Matching (Primary)](#32-recommended-strategy-keyword-matching-primary)
  - [3.3 Advanced Validation: Regex Pattern Matching](#33-advanced-validation-regex-pattern-matching)
  - [3.4 Optional: Fuzzy Matching for Tolerance](#34-optional-fuzzy-matching-for-tolerance)
  - [3.5 Handling Non-Deterministic Responses](#35-handling-non-deterministic-responses)
- [4. Parallel Execution](#4-parallel-execution)
  - [4.1 Recommended Approach: TPL with Parallel.ForEachAsync](#41-recommended-approach-tpl-with-parallelforeachasync)
  - [4.2 Managing Concurrent Copilot Sessions](#42-managing-concurrent-copilot-sessions)
  - [4.3 Aggregating Results Safely](#43-aggregating-results-safely)
  - [4.4 Rate Limiting & Throttling](#44-rate-limiting--throttling)
- [5. Spectre.Console Patterns](#5-spectreconsole-patterns)
  - [5.1 Progress Indicators](#51-progress-indicators)
  - [5.2 Table Output](#52-table-output)
  - [5.3 Panels & Boxes](#53-panels--boxes)
  - [5.4 Status Indicators](#54-status-indicators)
  - [5.5 Interactive Selection (MultiSelectionPrompt)](#55-interactive-selection-multiselectionprompt)
  - [5.6 Tree View for Suite Hierarchy](#56-tree-view-for-suite-hierarchy)
- [6. Test Isolation](#6-test-isolation)
  - [6.1 Session Management](#61-session-management)
  - [6.2 Cleanup Strategy](#62-cleanup-strategy)
  - [6.3 Temporary Directory Management](#63-temporary-directory-management)
- [7. Error Handling](#7-error-handling)
  - [7.1 Error Categories](#71-error-categories)
  - [7.2 Timeout Handling](#72-timeout-handling)
  - [7.3 API Error Handling](#73-api-error-handling)
  - [7.4 Auth Failure Handling](#74-auth-failure-handling)
  - [7.5 Resilience with Polly](#75-resilience-with-polly)
- [8. Process Management](#8-process-management)
  - [8.1 Recommended Library: CliWrap](#81-recommended-library-cliwrap)
  - [8.2 Alternative: System.Diagnostics.Process](#82-alternative-systemdiagnosticsprocess)
  - [8.3 Capturing Output](#83-capturing-output)
  - [8.4 Timeout Enforcement](#84-timeout-enforcement)
  - [8.5 Process Cleanup](#85-process-cleanup)
- [9. Testing the Test Harness](#9-testing-the-test-harness)
  - [9.1 Testing Strategy](#91-testing-strategy)
  - [9.2 Unit Tests](#92-unit-tests)
  - [9.3 Integration Tests](#93-integration-tests)
  - [9.4 Mock Implementations](#94-mock-implementations)
- [10. Configuration & Extensibility](#10-configuration--extensibility)
  - [10.1 Configuration Sources](#101-configuration-sources)
  - [10.2 External Test Definitions](#102-external-test-definitions)
  - [10.3 Plugin System (Future)](#103-plugin-system-future)
- [Recommendations Summary](#recommendations-summary)
- [References](#references)
- [Appendix: Code Examples](#appendix-code-examples)
- [Additional Research Findings](#additional-research-findings)
- [Implementation Roadmap](#implementation-roadmap)
- [Conclusion](#conclusion)

---

## Executive Summary

This document provides comprehensive research on implementing a self-testing module for the Lopen CLI tool. The module will enable automated testing of Lopen's functionality through real command execution and AI response validation. The research covers architecture patterns, test case definition, validation strategies, parallel execution, Spectre.Console UI patterns, test isolation, error handling, and extensibility.

---

## 1. TestRunner Architecture

### 1.1 Recommended Design Pattern: Command + Strategy Pattern

**Rationale:**
- **Command Pattern**: Each test case is represented as a command object that encapsulates all test execution logic (setup, execution, validation, cleanup)
- **Strategy Pattern**: Different validation strategies can be plugged in based on test type (keyword matching, regex, fuzzy matching)
- **Builder Pattern**: For constructing complex test configurations fluently

### 1.2 Core Architecture Components

```csharp
// Core abstraction for test execution
public interface ITestCase
{
    string TestId { get; }
    string Description { get; }
    string Suite { get; }
    Task<TestResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken);
}

// Test context provides shared resources
public sealed class TestContext
{
    public string Model { get; init; } = "gpt-5-mini";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public IServiceProvider Services { get; init; }
    public IAnsiConsole Console { get; init; }
}

// Test result encapsulates outcome
public sealed record TestResult
{
    public required string TestId { get; init; }
    public required TestStatus Status { get; init; }
    public required TimeSpan Duration { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
    public string? ResponsePreview { get; init; }
    public string? MatchedPattern { get; init; }
    public string? Error { get; init; }
}

public enum TestStatus
{
    Pass,
    Fail,
    Timeout,
    Error,
    Skipped
}

// Test runner orchestrates execution
public sealed class TestRunner
{
    private readonly IAnsiConsole _console;
    private readonly int _maxParallelism;
    
    public TestRunner(IAnsiConsole console, int maxParallelism = 4)
    {
        _console = console;
        _maxParallelism = maxParallelism;
    }
    
    public async Task<TestRunSummary> RunTestsAsync(
        IEnumerable<ITestCase> tests,
        TestContext context,
        CancellationToken cancellationToken = default)
    {
        var results = new ConcurrentBag<TestResult>();
        var summary = new TestRunSummary { StartTime = DateTimeOffset.Now };
        
        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var progressTask = ctx.AddTask("[green]Running tests[/]", maxValue: tests.Count());
                
                // Parallel execution with degree of parallelism
                await Parallel.ForEachAsync(
                    tests,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = _maxParallelism,
                        CancellationToken = cancellationToken
                    },
                    async (test, ct) =>
                    {
                        var result = await test.ExecuteAsync(context, ct);
                        results.Add(result);
                        progressTask.Increment(1);
                    });
            });
        
        summary.EndTime = DateTimeOffset.Now;
        summary.Results = results.ToList();
        return summary;
    }
}

public sealed class TestRunSummary
{
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
    public TimeSpan Duration => EndTime - StartTime;
    public List<TestResult> Results { get; set; } = [];
    
    public int Total => Results.Count;
    public int Passed => Results.Count(r => r.Status == TestStatus.Pass);
    public int Failed => Results.Count(r => r.Status == TestStatus.Fail);
    public int Errors => Results.Count(r => r.Status == TestStatus.Error);
    public int Timeouts => Results.Count(r => r.Status == TestStatus.Timeout);
}
```

### 1.3 Concrete Test Case Implementation

```csharp
// Chat command test case
public sealed class ChatCommandTestCase : ITestCase
{
    private readonly IProcessExecutor _processExecutor;
    private readonly IResponseValidator _validator;
    
    public required string TestId { get; init; }
    public required string Description { get; init; }
    public required string Suite { get; init; }
    public required string Input { get; init; }
    public required string[] ExpectedPatterns { get; init; }
    
    public ChatCommandTestCase(IProcessExecutor processExecutor, IResponseValidator validator)
    {
        _processExecutor = processExecutor;
        _validator = validator;
    }
    
    public async Task<TestResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.Now;
        
        try
        {
            // Create timeout cancellation token
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(context.Timeout);
            
            // Execute lopen chat command
            var result = await _processExecutor.ExecuteAsync(
                "lopen",
                ["chat", Input, "--model", context.Model],
                timeoutCts.Token);
            
            // Validate response
            var validation = _validator.Validate(result.StandardOutput, ExpectedPatterns);
            
            return new TestResult
            {
                TestId = TestId,
                Status = validation.IsValid ? TestStatus.Pass : TestStatus.Fail,
                Duration = DateTimeOffset.Now - startTime,
                StartTime = startTime,
                EndTime = DateTimeOffset.Now,
                ResponsePreview = TruncateResponse(result.StandardOutput, 200),
                MatchedPattern = validation.MatchedPattern,
                Error = validation.IsValid ? null : "No expected patterns found in response"
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CreateTimeoutResult(startTime);
        }
        catch (Exception ex)
        {
            return CreateErrorResult(startTime, ex);
        }
    }
    
    private TestResult CreateTimeoutResult(DateTimeOffset startTime) => new()
    {
        TestId = TestId,
        Status = TestStatus.Timeout,
        Duration = DateTimeOffset.Now - startTime,
        StartTime = startTime,
        EndTime = DateTimeOffset.Now,
        Error = $"Test exceeded timeout of {context.Timeout.TotalSeconds}s"
    };
    
    private TestResult CreateErrorResult(DateTimeOffset startTime, Exception ex) => new()
    {
        TestId = TestId,
        Status = TestStatus.Error,
        Duration = DateTimeOffset.Now - startTime,
        StartTime = startTime,
        EndTime = DateTimeOffset.Now,
        Error = ex.Message
    };
    
    private static string TruncateResponse(string response, int maxLength)
    {
        return response.Length <= maxLength 
            ? response 
            : response[..maxLength] + "...";
    }
}
```

---

## 2. Test Case Definition

### 2.1 Recommended Format: Embedded JSON with External YAML Support

**Rationale:**
- **Phase 1**: Embed test definitions in C# code for simplicity and type safety
- **Phase 2**: Support external YAML files for user extensibility
- JSON schema validation for external files

### 2.2 Test Definition Schema (JSON)

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "Lopen Test Suite",
  "type": "object",
  "properties": {
    "version": {
      "type": "string",
      "pattern": "^[0-9]+\\.[0-9]+$"
    },
    "suites": {
      "type": "array",
      "items": {
        "$ref": "#/definitions/TestSuite"
      }
    }
  },
  "required": ["version", "suites"],
  "definitions": {
    "TestSuite": {
      "type": "object",
      "properties": {
        "name": { "type": "string" },
        "description": { "type": "string" },
        "tests": {
          "type": "array",
          "items": { "$ref": "#/definitions/TestCase" }
        }
      },
      "required": ["name", "tests"]
    },
    "TestCase": {
      "type": "object",
      "properties": {
        "id": { "type": "string", "pattern": "^T-[A-Z]+-[0-9]+$" },
        "description": { "type": "string" },
        "type": {
          "type": "string",
          "enum": ["chat", "repl", "session", "auth"]
        },
        "input": { "type": "string" },
        "command": {
          "type": "array",
          "items": { "type": "string" }
        },
        "expected_patterns": {
          "type": "array",
          "items": { "type": "string" }
        },
        "match_mode": {
          "type": "string",
          "enum": ["any", "all", "regex"],
          "default": "any"
        },
        "timeout_seconds": {
          "type": "integer",
          "minimum": 1,
          "default": 30
        }
      },
      "required": ["id", "description", "type", "expected_patterns"]
    }
  }
}
```

### 2.3 Example Test Definition (YAML)

```yaml
version: "1.0"
suites:
  - name: chat
    description: Tests for chat command
    tests:
      - id: T-CHAT-01
        description: Basic arithmetic question
        type: chat
        input: "What is 2+2?"
        expected_patterns:
          - "4"
          - "four"
          - "equals"
        match_mode: any
        timeout_seconds: 30
      
      - id: T-CHAT-02
        description: Code generation request
        type: chat
        input: "Write hello world in C#"
        expected_patterns:
          - "Console.WriteLine"
          - "Hello"
        match_mode: any
        timeout_seconds: 45

  - name: session
    description: Tests for session management
    tests:
      - id: T-SESSION-01
        description: List all sessions
        type: session
        command: ["session", "list"]
        expected_patterns:
          - "Session ID"
          - "No sessions"
        match_mode: any
```

### 2.4 Embedded Test Definitions (C# Phase 1)

```csharp
public static class TestDefinitions
{
    public static IEnumerable<TestSuiteDefinition> GetBuiltInSuites()
    {
        yield return new TestSuiteDefinition
        {
            Name = "chat",
            Description = "Tests for chat command",
            Tests =
            [
                new TestCaseDefinition
                {
                    Id = "T-CHAT-01",
                    Description = "Basic arithmetic question",
                    Type = TestType.Chat,
                    Input = "What is 2+2?",
                    ExpectedPatterns = ["4", "four", "equals"],
                    MatchMode = MatchMode.Any,
                    TimeoutSeconds = 30
                },
                new TestCaseDefinition
                {
                    Id = "T-CHAT-02",
                    Description = "Code generation request",
                    Type = TestType.Chat,
                    Input = "Write hello world in C#",
                    ExpectedPatterns = ["Console.WriteLine", "Hello", "hello"],
                    MatchMode = MatchMode.Any,
                    TimeoutSeconds = 45
                }
            ]
        };
        
        yield return new TestSuiteDefinition
        {
            Name = "session",
            Description = "Tests for session management",
            Tests =
            [
                new TestCaseDefinition
                {
                    Id = "T-SESSION-01",
                    Description = "List all sessions",
                    Type = TestType.Session,
                    Command = ["session", "list"],
                    ExpectedPatterns = ["Session", "No sessions"],
                    MatchMode = MatchMode.Any
                }
            ]
        };
    }
}

public sealed record TestSuiteDefinition
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required List<TestCaseDefinition> Tests { get; init; }
}

public sealed record TestCaseDefinition
{
    public required string Id { get; init; }
    public required string Description { get; init; }
    public required TestType Type { get; init; }
    public string? Input { get; init; }
    public string[]? Command { get; init; }
    public required string[] ExpectedPatterns { get; init; }
    public MatchMode MatchMode { get; init; } = MatchMode.Any;
    public int TimeoutSeconds { get; init; } = 30;
}

public enum TestType { Chat, Repl, Session, Auth }
public enum MatchMode { Any, All, Regex }
```

---

## 3. AI Response Validation

### 3.1 Validation Strategy Pattern

```csharp
public interface IResponseValidator
{
    ValidationResult Validate(string response, string[] expectedPatterns);
}

public sealed record ValidationResult
{
    public required bool IsValid { get; init; }
    public string? MatchedPattern { get; init; }
    public List<string> MatchedPatterns { get; init; } = [];
}

// Keyword matching validator (case-insensitive substring)
public sealed class KeywordValidator : IResponseValidator
{
    public ValidationResult Validate(string response, string[] expectedPatterns)
    {
        var matches = new List<string>();
        
        foreach (var pattern in expectedPatterns)
        {
            if (response.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(pattern);
            }
        }
        
        return new ValidationResult
        {
            IsValid = matches.Count > 0,
            MatchedPattern = matches.FirstOrDefault(),
            MatchedPatterns = matches
        };
    }
}

// Regex validator for complex patterns
public sealed class RegexValidator : IResponseValidator
{
    public ValidationResult Validate(string response, string[] expectedPatterns)
    {
        var matches = new List<string>();
        
        foreach (var pattern in expectedPatterns)
        {
            try
            {
                if (Regex.IsMatch(response, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
                {
                    matches.Add(pattern);
                }
            }
            catch (RegexParseException)
            {
                // Invalid regex pattern, skip
                continue;
            }
        }
        
        return new ValidationResult
        {
            IsValid = matches.Count > 0,
            MatchedPattern = matches.FirstOrDefault(),
            MatchedPatterns = matches
        };
    }
}

// Composite validator supporting different match modes
public sealed class CompositeValidator : IResponseValidator
{
    private readonly MatchMode _matchMode;
    
    public CompositeValidator(MatchMode matchMode)
    {
        _matchMode = matchMode;
    }
    
    public ValidationResult Validate(string response, string[] expectedPatterns)
    {
        var validator = _matchMode == MatchMode.Regex 
            ? new RegexValidator() 
            : new KeywordValidator();
            
        var result = validator.Validate(response, expectedPatterns);
        
        // For "all" mode, check if all patterns matched
        if (_matchMode == MatchMode.All)
        {
            result = result with 
            { 
                IsValid = result.MatchedPatterns.Count == expectedPatterns.Length 
            };
        }
        
        return result;
    }
}
```

### 3.2 Handling Non-Deterministic AI Responses

**Strategies:**
1. **Flexible Pattern Matching**: Use multiple acceptable patterns per test
2. **Semantic Equivalence**: Accept variations ("4", "four", "equals 4")
3. **Partial Matching**: Match key concepts rather than exact phrasing
4. **Retry Logic**: Retry failed tests once to account for variance
5. **Confidence Thresholds**: Mark tests as "unstable" if they intermittently fail

```csharp
public sealed class RetryableTestCase : ITestCase
{
    private readonly ITestCase _innerTest;
    private readonly int _maxRetries;
    
    public RetryableTestCase(ITestCase innerTest, int maxRetries = 1)
    {
        _innerTest = innerTest;
        _maxRetries = maxRetries;
    }
    
    public async Task<TestResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken)
    {
        TestResult? lastResult = null;
        
        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            lastResult = await _innerTest.ExecuteAsync(context, cancellationToken);
            
            if (lastResult.Status == TestStatus.Pass)
            {
                return lastResult;
            }
            
            // Only retry on failure, not on error or timeout
            if (lastResult.Status != TestStatus.Fail)
            {
                break;
            }
        }
        
        return lastResult!;
    }
    
    // ITestCase properties delegated to inner test
    public string TestId => _innerTest.TestId;
    public string Description => _innerTest.Description;
    public string Suite => _innerTest.Suite;
}
```

---

## 4. Parallel Execution

### 4.1 Task Parallel Library (TPL) Approach

**Recommendation:** Use `Parallel.ForEachAsync` with controlled parallelism

```csharp
public sealed class ParallelTestRunner
{
    private readonly int _maxDegreeOfParallelism;
    private readonly SemaphoreSlim _rateLimiter;
    
    public ParallelTestRunner(int maxDegreeOfParallelism = 4, int requestsPerSecond = 10)
    {
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
        _rateLimiter = new SemaphoreSlim(requestsPerSecond, requestsPerSecond);
        
        // Release tokens periodically for rate limiting
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                
                // Release all tokens every second
                if (_rateLimiter.CurrentCount == 0)
                {
                    _rateLimiter.Release(requestsPerSecond);
                }
            }
        });
    }
    
    public async Task<List<TestResult>> RunTestsAsync(
        IEnumerable<ITestCase> tests,
        TestContext context,
        CancellationToken cancellationToken = default)
    {
        var results = new ConcurrentBag<TestResult>();
        
        await Parallel.ForEachAsync(
            tests,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _maxDegreeOfParallelism,
                CancellationToken = cancellationToken
            },
            async (test, ct) =>
            {
                // Apply rate limiting
                await _rateLimiter.WaitAsync(ct);
                
                try
                {
                    var result = await test.ExecuteAsync(context, ct);
                    results.Add(result);
                }
                finally
                {
                    // Token is released automatically by the periodic task
                }
            });
        
        return results.ToList();
    }
}
```

### 4.2 Thread-Safe Result Aggregation

```csharp
public sealed class ThreadSafeResultAggregator
{
    private readonly ConcurrentBag<TestResult> _results = new();
    private int _completed = 0;
    private int _total = 0;
    
    public void Initialize(int totalTests)
    {
        _total = totalTests;
    }
    
    public void AddResult(TestResult result)
    {
        _results.Add(result);
        Interlocked.Increment(ref _completed);
    }
    
    public int CompletedCount => _completed;
    public int TotalCount => _total;
    public double Progress => _total > 0 ? (double)_completed / _total : 0;
    
    public TestRunSummary GetSummary()
    {
        return new TestRunSummary
        {
            Results = _results.ToList()
        };
    }
}
```

---

## 5. Spectre.Console Patterns

### 5.1 Progress Display with Multiple Tasks

```csharp
public async Task DisplayProgressAsync(IEnumerable<ITestCase> tests, TestContext context)
{
    await AnsiConsole.Progress()
        .AutoClear(false)
        .HideCompleted(false)
        .Columns(
            new TaskDescriptionColumn { Alignment = Justify.Left },
            new ProgressBarColumn
            {
                CompletedStyle = new Style(Color.Green),
                RemainingStyle = new Style(Color.Grey),
                FinishedStyle = new Style(Color.Lime)
            },
            new PercentageColumn(),
            new RemainingTimeColumn(),
            new SpinnerColumn(Spinner.Known.Dots))
        .StartAsync(async ctx =>
        {
            var tasks = tests.Select(test =>
                ctx.AddTask($"[blue]{test.TestId}[/]: {test.Description}", maxValue: 100)
            ).ToList();
            
            // Execute tests and update progress
            await Parallel.ForEachAsync(
                tests.Zip(tasks),
                new ParallelOptions { MaxDegreeOfParallelism = 4 },
                async (pair, ct) =>
                {
                    var (test, progressTask) = pair;
                    
                    progressTask.StartTask();
                    var result = await test.ExecuteAsync(context, ct);
                    progressTask.Value = 100;
                    
                    // Update description based on result
                    progressTask.Description = result.Status switch
                    {
                        TestStatus.Pass => $"[green]✓[/] {test.TestId}: {test.Description}",
                        TestStatus.Fail => $"[red]✗[/] {test.TestId}: {test.Description}",
                        TestStatus.Timeout => $"[yellow]⏱[/] {test.TestId}: {test.Description}",
                        _ => $"[red]![/] {test.TestId}: {test.Description}"
                    };
                });
        });
}
```

### 5.2 Results Table with Status Indicators

```csharp
public void DisplayResultsTable(TestRunSummary summary)
{
    var table = new Table()
        .RoundedBorder()
        .BorderColor(Color.Grey)
        .AddColumn(new TableColumn("[yellow]Test ID[/]").LeftAligned())
        .AddColumn(new TableColumn("[yellow]Status[/]").Centered())
        .AddColumn(new TableColumn("[yellow]Duration[/]").RightAligned())
        .AddColumn(new TableColumn("[yellow]Details[/]").LeftAligned());
    
    foreach (var result in summary.Results.OrderBy(r => r.TestId))
    {
        var statusMarkup = result.Status switch
        {
            TestStatus.Pass => "[green]✓ PASS[/]",
            TestStatus.Fail => "[red]✗ FAIL[/]",
            TestStatus.Timeout => "[yellow]⏱ TIMEOUT[/]",
            TestStatus.Error => "[red]! ERROR[/]",
            TestStatus.Skipped => "[grey]- SKIP[/]",
            _ => "[grey]?[/]"
        };
        
        var duration = $"{result.Duration.TotalSeconds:F1}s";
        
        var details = result.Status == TestStatus.Pass
            ? result.MatchedPattern ?? ""
            : result.Error ?? "";
        
        // Truncate long error messages
        if (details.Length > 50)
        {
            details = details[..47] + "...";
        }
        
        table.AddRow(
            result.TestId,
            statusMarkup,
            duration,
            Markup.Escape(details));
    }
    
    AnsiConsole.Write(table);
}
```

### 5.3 Summary Panel with Statistics

```csharp
public void DisplaySummaryPanel(TestRunSummary summary)
{
    var panel = new Panel(new Rows(
        new Markup($"[green]✓ Passed:[/]  {summary.Passed}/{summary.Total}"),
        new Markup($"[red]✗ Failed:[/]  {summary.Failed}/{summary.Total}"),
        new Markup($"[yellow]⏱ Timeouts:[/] {summary.Timeouts}/{summary.Total}"),
        new Markup($"[red]! Errors:[/]  {summary.Errors}/{summary.Total}"),
        new Rule(),
        new Markup($"[blue]⏱ Duration:[/] {summary.Duration.TotalSeconds:F1}s")
    ))
    {
        Header = new PanelHeader("[yellow bold]Test Run Summary[/]", Justify.Center),
        Border = BoxBorder.Rounded,
        BorderStyle = summary.Failed > 0 || summary.Errors > 0 
            ? new Style(Color.Red) 
            : new Style(Color.Green),
        Padding = new Padding(2, 1)
    };
    
    AnsiConsole.Write(panel);
}
```

### 5.4 Interactive Suite Selection

```csharp
public async Task<List<string>> SelectSuitesInteractivelyAsync(List<TestSuiteDefinition> suites)
{
    var selectedSuites = AnsiConsole.Prompt(
        new MultiSelectionPrompt<string>()
            .Title("[yellow]Select test suites to run:[/]")
            .PageSize(10)
            .MoreChoicesText("[grey](Move up and down to reveal more suites)[/]")
            .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]")
            .AddChoices(suites.Select(s => $"{s.Name} - {s.Description ?? ""}"))
    );
    
    return selectedSuites
        .Select(s => s.Split(" - ")[0])
        .ToList();
}
```

---

## 6. Test Isolation

### 6.1 IAsyncDisposable Pattern for Test Cleanup

```csharp
public sealed class IsolatedTestCase : ITestCase, IAsyncDisposable
{
    private readonly ITestCase _innerTest;
    private readonly IsolationContext _isolation;
    
    public IsolatedTestCase(ITestCase innerTest, IsolationContext isolation)
    {
        _innerTest = innerTest;
        _isolation = isolation;
    }
    
    public async Task<TestResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken)
    {
        await _isolation.SetupAsync(cancellationToken);
        
        try
        {
            return await _innerTest.ExecuteAsync(context, cancellationToken);
        }
        finally
        {
            await _isolation.CleanupAsync(cancellationToken);
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        await _isolation.DisposeAsync();
    }
    
    public string TestId => _innerTest.TestId;
    public string Description => _innerTest.Description;
    public string Suite => _innerTest.Suite;
}

public sealed class IsolationContext : IAsyncDisposable
{
    private readonly string _tempDirectory;
    private readonly List<string> _sessionIds = new();
    
    public IsolationContext()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid():N}");
    }
    
    public async Task SetupAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_tempDirectory);
        // Additional setup (e.g., create test session)
        await Task.CompletedTask;
    }
    
    public async Task CleanupAsync(CancellationToken cancellationToken)
    {
        // Clean up sessions created during test
        foreach (var sessionId in _sessionIds)
        {
            await DeleteSessionAsync(sessionId, cancellationToken);
        }
        
        // Clean up temp directory
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
    
    private async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        // Execute: lopen session delete {sessionId}
        await Task.CompletedTask; // Placeholder
    }
    
    public async ValueTask DisposeAsync()
    {
        await CleanupAsync(CancellationToken.None);
    }
}
```

---

## 7. Error Handling

### 7.1 Polly Resilience Policies

```csharp
using Polly;
using Polly.Timeout;
using Polly.Retry;

public sealed class ResilientProcessExecutor : IProcessExecutor
{
    private readonly IProcessExecutor _inner;
    private readonly ResiliencePipeline _pipeline;
    
    public ResilientProcessExecutor(IProcessExecutor inner)
    {
        _inner = inner;
        _pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(60))
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>()
            })
            .Build();
    }
    
    public async Task<ProcessResult> ExecuteAsync(
        string command,
        string[] arguments,
        CancellationToken cancellationToken)
    {
        return await _pipeline.ExecuteAsync(
            async ct => await _inner.ExecuteAsync(command, arguments, ct),
            cancellationToken);
    }
}
```

### 7.2 Comprehensive Error Categorization

```csharp
public enum ErrorCategory
{
    Timeout,
    AuthenticationFailure,
    NetworkError,
    ApiRateLimitExceeded,
    ProcessNotFound,
    InvalidCommand,
    UnexpectedError
}

public sealed class TestErrorHandler
{
    public ErrorCategory CategorizeError(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => ErrorCategory.Timeout,
            UnauthorizedAccessException => ErrorCategory.AuthenticationFailure,
            HttpRequestException => ErrorCategory.NetworkError,
            FileNotFoundException => ErrorCategory.ProcessNotFound,
            _ => ErrorCategory.UnexpectedError
        };
    }
    
    public bool ShouldAbortTestRun(ErrorCategory category)
    {
        // Abort entire run on auth failures
        return category == ErrorCategory.AuthenticationFailure;
    }
    
    public string GetUserFriendlyMessage(ErrorCategory category)
    {
        return category switch
        {
            ErrorCategory.Timeout => "Test timed out. Consider increasing timeout with --timeout flag.",
            ErrorCategory.AuthenticationFailure => "Authentication failed. Please run 'lopen auth login'.",
            ErrorCategory.NetworkError => "Network error. Check your internet connection.",
            ErrorCategory.ApiRateLimitExceeded => "API rate limit exceeded. Please wait and try again.",
            ErrorCategory.ProcessNotFound => "Lopen CLI not found. Ensure it's in your PATH.",
            ErrorCategory.InvalidCommand => "Invalid command syntax.",
            _ => "An unexpected error occurred."
        };
    }
}
```

---

## 8. Process Management

### 8.1 CliWrap for Process Execution

**Recommendation:** Use CliWrap library for robust process management

```csharp
using CliWrap;
using CliWrap.Buffered;

public sealed class CliWrapProcessExecutor : IProcessExecutor
{
    public async Task<ProcessResult> ExecuteAsync(
        string command,
        string[] arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await Cli.Wrap(command)
                .WithArguments(arguments)
                .WithValidation(CommandResultValidation.None) // Handle validation ourselves
                .ExecuteBufferedAsync(cancellationToken);
            
            return new ProcessResult
            {
                ExitCode = result.ExitCode,
                StandardOutput = result.StandardOutput,
                StandardError = result.StandardError,
                StartTime = result.StartTime,
                ExitTime = result.ExitTime,
                RunTime = result.RunTime
            };
        }
        catch (OperationCanceledException)
        {
            throw; // Rethrow timeout exceptions
        }
        catch (Exception ex)
        {
            throw new ProcessExecutionException($"Failed to execute {command}", ex);
        }
    }
}

public sealed record ProcessResult
{
    public required int ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset ExitTime { get; init; }
    public TimeSpan RunTime { get; init; }
    
    public bool IsSuccess => ExitCode == 0;
}
```

### 8.2 Alternative: System.Diagnostics.Process with Timeout

```csharp
public sealed class SystemProcessExecutor : IProcessExecutor
{
    public async Task<ProcessResult> ExecuteAsync(
        string command,
        string[] arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = string.Join(" ", arguments.Select(EscapeArgument)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        
        process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };
        
        var startTime = DateTimeOffset.Now;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        // Wait for process with cancellation support
        await process.WaitForExitAsync(cancellationToken);
        
        var exitTime = DateTimeOffset.Now;
        
        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = outputBuilder.ToString(),
            StandardError = errorBuilder.ToString(),
            StartTime = startTime,
            ExitTime = exitTime,
            RunTime = exitTime - startTime
        };
    }
    
    private static string EscapeArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        if (arg.Contains(' ') || arg.Contains('"'))
        {
            return $"\"{arg.Replace("\"", "\\\"")}\"";
        }
        return arg;
    }
}
```

---

## 9. Testing the Test Harness

### 9.1 Unit Testing Strategy

```csharp
using Xunit;
using Moq;

public class TestRunnerTests
{
    [Fact]
    public async Task RunTestsAsync_AllTestsPass_ReturnsSuccessSummary()
    {
        // Arrange
        var mockTest = new Mock<ITestCase>();
        mockTest.Setup(t => t.ExecuteAsync(It.IsAny<TestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestResult
            {
                TestId = "T-001",
                Status = TestStatus.Pass,
                Duration = TimeSpan.FromSeconds(1)
            });
        
        var runner = new TestRunner(AnsiConsole.Console, maxParallelism: 1);
        var context = new TestContext { Model = "gpt-5-mini" };
        
        // Act
        var summary = await runner.RunTestsAsync(new[] { mockTest.Object }, context);
        
        // Assert
        Assert.Equal(1, summary.Total);
        Assert.Equal(1, summary.Passed);
        Assert.Equal(0, summary.Failed);
    }
    
    [Fact]
    public async Task RunTestsAsync_TestTimeout_ReturnsTimeoutStatus()
    {
        // Arrange
        var mockTest = new Mock<ITestCase>();
        mockTest.Setup(t => t.ExecuteAsync(It.IsAny<TestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestResult
            {
                TestId = "T-002",
                Status = TestStatus.Timeout,
                Duration = TimeSpan.FromSeconds(30)
            });
        
        var runner = new TestRunner(AnsiConsole.Console, maxParallelism: 1);
        var context = new TestContext { Timeout = TimeSpan.FromSeconds(5) };
        
        // Act
        var summary = await runner.RunTestsAsync(new[] { mockTest.Object }, context);
        
        // Assert
        Assert.Equal(1, summary.Timeouts);
    }
}
```

### 9.2 Integration Testing Approach

```csharp
[Collection("Integration Tests")]
public class SelfTestIntegrationTests : IAsyncLifetime
{
    private string _testDirectory;
    
    public async Task InitializeAsync()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"lopen-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        
        // Ensure lopen is authenticated
        await EnsureAuthenticatedAsync();
    }
    
    [Fact]
    public async Task LopenTestSelf_WithValidAuth_ExecutesSuccessfully()
    {
        // Arrange
        var executor = new CliWrapProcessExecutor();
        
        // Act
        var result = await executor.ExecuteAsync(
            "lopen",
            ["test", "self", "--filter", "T-CHAT-01"],
            CancellationToken.None);
        
        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("PASS", result.StandardOutput);
    }
    
    public async Task DisposeAsync()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
        await Task.CompletedTask;
    }
    
    private async Task EnsureAuthenticatedAsync()
    {
        var executor = new CliWrapProcessExecutor();
        var result = await executor.ExecuteAsync("lopen", ["auth", "status"], CancellationToken.None);
        
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Lopen is not authenticated. Run 'lopen auth login' first.");
        }
    }
}
```

---

## 10. Configuration & Extensibility

### 10.1 Configuration Priority

1. Command-line flags (`--model`, `--timeout`)
2. Environment variables (`LOPEN_TEST_MODEL`, `LOPEN_TEST_TIMEOUT`)
3. Configuration file (`~/.lopen/test-config.json`)
4. Default values

```csharp
public sealed class TestConfiguration
{
    public string Model { get; set; } = "gpt-5-mini";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxParallelism { get; set; } = 4;
    public int RequestsPerSecond { get; set; } = 10;
    public string? CustomTestsDirectory { get; set; }
    
    public static TestConfiguration Load()
    {
        var config = new TestConfiguration();
        
        // Load from config file
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".lopen",
            "test-config.json");
        
        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            config = JsonSerializer.Deserialize<TestConfiguration>(json) ?? config;
        }
        
        // Override with environment variables
        config.Model = Environment.GetEnvironmentVariable("LOPEN_TEST_MODEL") ?? config.Model;
        
        if (int.TryParse(Environment.GetEnvironmentVariable("LOPEN_TEST_TIMEOUT"), out var timeout))
        {
            config.TimeoutSeconds = timeout;
        }
        
        return config;
    }
}
```

### 10.2 External Test Loader

```csharp
public sealed class ExternalTestLoader
{
    private readonly string _testsDirectory;
    
    public ExternalTestLoader(string testsDirectory)
    {
        _testsDirectory = testsDirectory;
    }
    
    public async Task<List<TestSuiteDefinition>> LoadExternalTestsAsync()
    {
        var suites = new List<TestSuiteDefinition>();
        
        if (!Directory.Exists(_testsDirectory))
        {
            return suites;
        }
        
        var testFiles = Directory.GetFiles(_testsDirectory, "*.yaml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(_testsDirectory, "*.yml", SearchOption.AllDirectories));
        
        foreach (var file in testFiles)
        {
            try
            {
                var yaml = await File.ReadAllTextAsync(file);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();
                
                var testFile = deserializer.Deserialize<TestFileDefinition>(yaml);
                suites.AddRange(testFile.Suites);
            }
            catch (Exception ex)
            {
                // Log warning and continue
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Failed to load {file}: {ex.Message}");
            }
        }
        
        return suites;
    }
}
```

---

## Recommendations Summary

### Architecture
- **Use Command Pattern** for test cases
- **Use Strategy Pattern** for validation
- **Use Builder Pattern** for configuration
- **Use TPL** for parallel execution (Parallel.ForEachAsync)

### Test Definitions
- **Phase 1:** Embedded in C# for simplicity
- **Phase 2:** External YAML with JSON schema validation
- **Format:** YAML for readability, JSON schema for validation

### Validation
- **Primary:** Case-insensitive keyword matching (simple substring)
- **Advanced:** Regex for complex patterns
- **Strategy:** Support multiple patterns with "any"/"all" modes
- **Non-determinism:** Retry failed tests once, use flexible patterns

### Parallel Execution
- **Parallelism:** 4 concurrent tests (configurable)
- **Rate Limiting:** 10 requests/second to avoid API throttling
- **Thread Safety:** ConcurrentBag for result aggregation
- **Cancellation:** Full cancellation token support

### Spectre.Console
- **Progress:** Use Progress with custom columns
- **Results:** Table with colored status indicators
- **Summary:** Panel with statistics
- **Interactive:** MultiSelectionPrompt for suite selection
- **Status:** Status display for long operations

### Test Isolation
- **Pattern:** IAsyncDisposable for cleanup
- **Temp Data:** Unique temp directory per test
- **Session Cleanup:** Delete test sessions after completion
- **Error Handling:** Cleanup in finally blocks

### Error Handling
- **Resilience:** Polly for retry/timeout policies
- **Categorization:** Categorize errors for better UX
- **Abort Strategy:** Abort on auth failures
- **Logging:** Structured logging with Serilog

### Process Management
- **Library:** CliWrap (recommended) or System.Diagnostics.Process
- **Timeout:** Integrated cancellation token support
- **Output Capture:** Buffered execution for small outputs
- **Streaming:** Stream for large outputs

### Testing
- **Unit Tests:** Mock ITestCase, IProcessExecutor
- **Integration Tests:** Real lopen commands with test isolation
- **CI/CD:** JSON output format for automation
- **Coverage:** Aim for 80%+ coverage of test harness

### Configuration
- **Priority:** CLI flags > env vars > config file > defaults
- **Extensibility:** Plugin directory for custom test files
- **Schema:** JSON schema validation for external tests

---

## References

1. **Microsoft .NET Documentation**
   - [Testing in .NET](https://learn.microsoft.com/en-us/dotnet/core/testing/)
   - [Task Parallel Library](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl)
   - [Dependency Injection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
   - [IAsyncDisposable](https://learn.microsoft.com/en-us/dotnet/api/system.iasyncdisposable)

2. **Spectre.Console**
   - [Documentation](https://spectreconsole.net/)
   - [Progress Display](https://spectreconsole.net/console/live/progress)
   - [Tables](https://spectreconsole.net/console/widgets/table)
   - [Status Display](https://spectreconsole.net/console/live/status)

3. **CliWrap**
   - [GitHub Repository](https://github.com/Tyrrrz/CliWrap)
   - [NuGet Package](https://www.nuget.org/packages/CliWrap)

4. **Polly**
   - [GitHub Repository](https://github.com/App-vNext/Polly)
   - [Documentation](https://www.pollydocs.org/)
   - [NuGet Package](https://www.nuget.org/packages/Polly)

5. **Best Practices**
   - [Unit Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
   - [Testing Pyramid](https://martinfowler.com/articles/practical-test-pyramid.html)

---

## Appendix: Code Examples

See inline code snippets throughout the document for complete implementation examples.

---

**Document Version:** 1.0  
**Last Updated:** $(date +%Y-%m-%d)


---

## Additional Research Findings

### Testing Framework Comparison

#### xUnit.net
- **Philosophy:** Simplicity and extensibility
- **Test Discovery:** Attributes (`[Fact]`, `[Theory]`)
- **Setup/Teardown:** Constructor/`IDisposable` pattern
- **Parallel Execution:** Built-in, test classes run in parallel by default
- **Assertions:** `Assert.Equal()`, `Assert.True()`, etc.
- **Best For:** Modern .NET projects, projects valuing simplicity

#### NUnit
- **Philosophy:** Feature-rich with extensive attribute system
- **Test Discovery:** Attributes (`[Test]`, `[TestCase]`)
- **Setup/Teardown:** `[SetUp]`, `[TearDown]`, `[OneTimeSetUp]`, `[OneTimeTearDown]`
- **Parallel Execution:** `[Parallelizable]` attribute
- **Assertions:** Constraint-based (`Assert.That(x, Is.EqualTo(y))`)
- **Best For:** Complex test scenarios, migrating from NUnit 2/3

#### MSTest
- **Philosophy:** Microsoft's official framework
- **Test Discovery:** Attributes (`[TestMethod]`, `[DataTestMethod]`)
- **Setup/Teardown:** `[TestInitialize]`, `[TestCleanup]`
- **Parallel Execution:** Assembly-level `[assembly: Parallelize]`
- **Assertions:** `Assert.AreEqual()`, `Assert.IsTrue()`, etc.
- **Best For:** Visual Studio integration, enterprise projects

**Recommendation for Lopen:** Use xUnit.net for testing the test harness itself due to its modern approach and excellent async support.

---

### Process Execution Library Comparison

#### CliWrap (Recommended)
```csharp
// Pros:
// - Fluent API
// - Built-in timeout support
// - Buffered and streaming execution
// - Automatic disposal
// - Cancellation token support
// - Cross-platform

var result = await Cli.Wrap("lopen")
    .WithArguments(["chat", "Hello", "--model", "gpt-5-mini"])
    .WithWorkingDirectory("/tmp")
    .WithEnvironmentVariables(new Dictionary<string, string>
    {
        ["LOPEN_TEST_MODE"] = "true"
    })
    .WithValidation(CommandResultValidation.None)
    .ExecuteBufferedAsync(cancellationToken);
```

#### Medallion.Shell
- **Note:** Repository appears to be unavailable or relocated
- Alternative: Use CliWrap or System.Diagnostics.Process

#### System.Diagnostics.Process (Fallback)
```csharp
// Pros:
// - Built-in, no dependencies
// - Full control

// Cons:
// - More boilerplate
// - Manual timeout handling
// - Complex stream handling

var psi = new ProcessStartInfo
{
    FileName = "lopen",
    Arguments = "chat \"Hello\"",
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    CreateNoWindow = true
};

using var process = Process.Start(psi);
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

await process.WaitForExitAsync(cts.Token);

var output = await outputTask;
var error = await errorTask;
```

---

### JSON Schema Validation Libraries

#### JSON.NET Schema (Newtonsoft)
```csharp
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Linq;

public class JsonSchemaValidator
{
    private readonly JSchema _schema;
    
    public JsonSchemaValidator(string schemaJson)
    {
        _schema = JSchema.Parse(schemaJson);
    }
    
    public (bool IsValid, IList<string> Errors) Validate(string json)
    {
        var obj = JObject.Parse(json);
        var isValid = obj.IsValid(_schema, out IList<string> errors);
        return (isValid, errors);
    }
}
```

#### System.Text.Json with JSON Schema (Community)
- Use `JsonSchema.Net` package for System.Text.Json compatibility
- Native support coming in future .NET versions

---

### Logging Integration

#### Serilog for Structured Logging
```csharp
using Serilog;
using Serilog.Events;

public static class TestLogging
{
    public static ILogger CreateLogger(string testId, string outputPath)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(outputPath, $"{testId}-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
            .Enrich.WithProperty("TestId", testId)
            .CreateLogger();
    }
}

// Usage in test
public async Task<TestResult> ExecuteAsync(TestContext context, CancellationToken ct)
{
    var log = TestLogging.CreateLogger(TestId, context.LogDirectory);
    
    log.Information("Starting test {TestId}: {Description}", TestId, Description);
    
    try
    {
        // Execute test
        log.Debug("Executing command: lopen {Args}", string.Join(" ", args));
        var result = await _executor.ExecuteAsync("lopen", args, ct);
        log.Debug("Received output: {Output}", result.StandardOutput);
        
        return new TestResult { /* ... */ };
    }
    catch (Exception ex)
    {
        log.Error(ex, "Test {TestId} failed with exception", TestId);
        throw;
    }
}
```

---

### Fuzzy Matching for AI Responses

#### FuzzySharp Library
```csharp
using FuzzySharp;

public class FuzzyResponseValidator : IResponseValidator
{
    private readonly int _threshold;
    
    public FuzzyResponseValidator(int threshold = 80)
    {
        _threshold = threshold;
    }
    
    public ValidationResult Validate(string response, string[] expectedPatterns)
    {
        var matches = new List<string>();
        
        foreach (var pattern in expectedPatterns)
        {
            var ratio = Fuzz.PartialRatio(response.ToLowerInvariant(), pattern.ToLowerInvariant());
            
            if (ratio >= _threshold)
            {
                matches.Add($"{pattern} (similarity: {ratio}%)");
            }
        }
        
        return new ValidationResult
        {
            IsValid = matches.Count > 0,
            MatchedPattern = matches.FirstOrDefault(),
            MatchedPatterns = matches
        };
    }
}
```

---

### Advanced Spectre.Console Patterns

#### Custom Progress Column
```csharp
public class StatusIconColumn : ProgressColumn
{
    protected override bool NoWrap => true;
    
    public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
    {
        var icon = task.IsFinished
            ? task.Value >= task.MaxValue ? "✓" : "✗"
            : "⋯";
            
        var color = task.IsFinished
            ? task.Value >= task.MaxValue ? Color.Green : Color.Red
            : Color.Yellow;
            
        return new Markup($"[{color}]{icon}[/]");
    }
}

// Usage
await AnsiConsole.Progress()
    .Columns(
        new StatusIconColumn(),
        new TaskDescriptionColumn(),
        new ProgressBarColumn(),
        new PercentageColumn())
    .StartAsync(async ctx => { /* ... */ });
```

#### Live Display with Test Status
```csharp
public async Task RunTestsWithLiveUpdatesAsync(IEnumerable<ITestCase> tests)
{
    var table = new Table()
        .RoundedBorder()
        .AddColumn("Test ID")
        .AddColumn("Status")
        .AddColumn("Duration");
    
    await AnsiConsole.Live(table)
        .StartAsync(async ctx =>
        {
            foreach (var test in tests)
            {
                var row = table.AddRow(test.TestId, "[yellow]Running...[/]", "0.0s");
                ctx.Refresh();
                
                var sw = Stopwatch.StartNew();
                var result = await test.ExecuteAsync(context, CancellationToken.None);
                sw.Stop();
                
                // Update the row
                var status = result.Status == TestStatus.Pass ? "[green]✓ PASS[/]" : "[red]✗ FAIL[/]";
                row.Cells[1] = new Markup(status);
                row.Cells[2] = new Text($"{sw.Elapsed.TotalSeconds:F1}s");
                
                ctx.Refresh();
            }
        });
}
```

---

### CI/CD Integration

#### JSON Output Format for GitHub Actions
```csharp
public class JsonTestReporter
{
    public string GenerateReport(TestRunSummary summary)
    {
        var report = new
        {
            summary = new
            {
                total = summary.Total,
                passed = summary.Passed,
                failed = summary.Failed,
                errors = summary.Errors,
                timeouts = summary.Timeouts,
                duration_seconds = summary.Duration.TotalSeconds,
                start_time = summary.StartTime,
                end_time = summary.EndTime
            },
            tests = summary.Results.Select(r => new
            {
                test_id = r.TestId,
                status = r.Status.ToString().ToLowerInvariant(),
                duration_seconds = r.Duration.TotalSeconds,
                error = r.Error,
                matched_pattern = r.MatchedPattern
            })
        };
        
        return JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
    }
}
```

#### GitHub Actions Workflow Example
```yaml
name: Lopen Self-Test
on: [push, pull_request]

jobs:
  self-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      
      - name: Build Lopen
        run: dotnet build -c Release
      
      - name: Authenticate with GitHub Copilot
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          gh auth login --with-token <<< "$GITHUB_TOKEN"
          gh copilot auth
      
      - name: Run Self-Tests
        run: |
          dotnet run --project src/Lopen -- test self --format json > test-results.json
      
      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: test-results
          path: test-results.json
      
      - name: Check Test Results
        run: |
          if jq -e '.summary.failed > 0' test-results.json; then
            echo "Tests failed!"
            exit 1
          fi
```

---

### Performance Considerations

#### Memory-Efficient Test Execution
```csharp
public class StreamingTestRunner
{
    public async Task RunLargeTestSuiteAsync(IAsyncEnumerable<ITestCase> tests)
    {
        // Stream tests instead of loading all into memory
        await foreach (var test in tests.WithCancellation(CancellationToken.None))
        {
            var result = await test.ExecuteAsync(context, CancellationToken.None);
            
            // Write result immediately instead of accumulating
            await WriteResultToFileAsync(result);
            
            // Free memory
            GC.Collect(0, GCCollectionMode.Optimized);
        }
    }
}
```

#### Connection Pooling for Rate Limiting
```csharp
public class RateLimitedExecutor
{
    private readonly SemaphoreSlim _semaphore;
    private readonly IProcessExecutor _inner;
    
    public RateLimitedExecutor(IProcessExecutor inner, int maxConcurrent = 4)
    {
        _inner = inner;
        _semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }
    
    public async Task<ProcessResult> ExecuteAsync(
        string command,
        string[] arguments,
        CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        
        try
        {
            // Add delay to avoid API rate limits
            await Task.Delay(100, cancellationToken);
            return await _inner.ExecuteAsync(command, arguments, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

---

## Implementation Roadmap

### Phase 1: Core Infrastructure (Week 1)
- [ ] Implement `ITestCase` interface and base classes
- [ ] Implement `TestRunner` with parallel execution
- [ ] Implement `CliWrapProcessExecutor`
- [ ] Implement basic `KeywordValidator`
- [ ] Add Spectre.Console progress display

### Phase 2: Test Definitions (Week 2)
- [ ] Define embedded test suites for chat, session, auth
- [ ] Implement test case factory
- [ ] Add test filtering logic
- [ ] Implement interactive mode with Spectre.Console

### Phase 3: Advanced Features (Week 3)
- [ ] Add timeout and cancellation support
- [ ] Implement Polly resilience policies
- [ ] Add structured logging with Serilog
- [ ] Implement JSON output format

### Phase 4: Extensibility (Week 4)
- [ ] Add YAML test definition loader
- [ ] Implement JSON schema validation
- [ ] Add custom validator plugin support
- [ ] Create user documentation

### Phase 5: Testing & Polish (Week 5)
- [ ] Unit tests for test harness (xUnit)
- [ ] Integration tests with real lopen commands
- [ ] CI/CD pipeline integration
- [ ] Performance optimization

---

## Conclusion

This research provides a comprehensive foundation for implementing the Lopen self-testing module. The recommended architecture leverages:

- **Command Pattern** for flexible test case design
- **Strategy Pattern** for pluggable validation
- **CliWrap** for robust process execution
- **Spectre.Console** for rich terminal UI
- **Polly** for resilience and error handling
- **xUnit** for testing the test harness itself

The design prioritizes:
- **Simplicity:** Embedded tests in Phase 1, external files in Phase 2
- **Extensibility:** Plugin architecture for validators and test loaders
- **Performance:** Parallel execution with rate limiting
- **User Experience:** Rich terminal UI with Spectre.Console
- **Reliability:** Comprehensive error handling and test isolation

Next steps: Begin implementation with Phase 1 (Core Infrastructure).

---

## Implementation Gap Analysis (January 26, 2025)

### Current Implementation Status

The Testing module has been **substantially implemented** with the following components in place:

#### ✅ Completed Features

1. **Core Architecture** (Phase 1)
   - ✅ `ITestCase` interface with `TestId`, `Description`, `Suite`, `ExecuteAsync()`
   - ✅ `TestRunner` with parallel execution using `Parallel.ForEachAsync`
   - ✅ `CommandTestCase` for executing CLI commands via `System.Diagnostics.Process`
   - ✅ `TestContext` with Model, Timeout, Verbose, LopenPath configuration
   - ✅ `TestResult` with comprehensive execution metadata
   - ✅ `TestRunSummary` with aggregated results and statistics
   - ✅ `KeywordValidator` implementing `ITestValidator` for pattern matching
   - ✅ `TestStatus` enum (Pass, Fail, Timeout, Error, Skipped)

2. **Test Suites** (Phase 2)
   - ✅ `TestSuiteRegistry` with suite management and filtering
   - ✅ `CoreTestSuite` (T-CORE-01: version, T-CORE-02: help)
   - ✅ `AuthTestSuite` (T-AUTH-01: auth status)
   - ✅ `SessionTestSuite` (T-SESSION-01: list sessions)
   - ✅ `ChatTestSuite` (T-CHAT-01: basic math, T-CHAT-02: greeting)

3. **CLI Integration**
   - ✅ `lopen test self` command in Program.cs
   - ✅ `--verbose/-v` flag for detailed output
   - ✅ `--filter` pattern matching (ID, suite, description)
   - ✅ `--model/-m` override (default: gpt-5-mini)
   - ✅ `--timeout/-t` per-test timeout configuration
   - ✅ `--format/-f` for text/json output
   - ✅ `--interactive/-i` for test selection
   - ✅ Exit code 0 on success, 1 on failure

4. **Output & UI** (Phase 3)
   - ✅ `TestOutputService` using ConsoleOutput abstraction
   - ✅ Header display with model and test count
   - ✅ Table display for test results
   - ✅ Summary panel with pass/fail counts
   - ✅ Verbose output with per-test details
   - ✅ JSON output format with full result serialization
   - ✅ Status symbols (✓ ✗ ⏱ ⚠)

5. **Interactive Mode**
   - ✅ `IInteractiveTestSelector` interface
   - ✅ `SpectreInteractiveTestSelector` with multi-select prompts
   - ✅ `MockInteractiveTestSelector` for testing
   - ✅ Suite and test selection UI
   - ✅ Model confirmation

6. **Error Handling**
   - ✅ Timeout handling with `CancellationTokenSource`
   - ✅ Exit code validation
   - ✅ Exception catching with Error status
   - ✅ Cancellation support (Ctrl+C)

7. **Test Coverage**
   - ✅ 60+ unit tests for Testing module components
   - ✅ Tests for: KeywordValidator, TestRunner, TestOutputService, TestContext, TestResult, TestRunSummary, TestSuiteRegistry, InteractiveTestSelector

#### 🟡 Partially Implemented / Missing Features

Based on SPECIFICATION.md acceptance criteria marked incomplete (`- [ ]`):

### 1. **Per-Test Logging with Timestamps** (Phase 2)
   **Status:** ❌ NOT IMPLEMENTED  
   **Specification Line:** Line 39 in SPECIFICATION.md
   
   **Current State:**
   - `TestResult` has `StartTime` and `EndTime` properties (DateTimeOffset)
   - Timestamps are captured but NOT displayed in output
   - Verbose output shows: `✓ T-CHAT-01: Basic math question (1.2s)` 
   - Missing: Actual timestamp display like `[2025-01-26 10:30:45.123]`
   
   **Required Changes:**
   
   ```csharp
   // In TestOutputService.cs - DisplayVerboseResult method (line 81-103)
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
       
       // ADD TIMESTAMP
       var timestamp = result.StartTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
       
       // MODIFY OUTPUT LINE
       _output.WriteLine($"[{timestamp}] {statusSymbol} {result.TestId}: {result.Description} ({result.Duration.TotalSeconds:F1}s)");
       
       // Rest remains same...
   }
   ```
   
   **Alternative Implementation:**
   - Add `--show-timestamps` flag to control timestamp display
   - Store per-test logs in temporary files (as mentioned in spec line 186)
   - Display timestamps only in verbose mode
   
   **Estimated Effort:** 2-4 hours
   
   **Test Changes Needed:**
   - Update `TestOutputServiceTests.DisplayVerboseResult_ShowsPassingTest` to verify timestamp format
   - Add test for timestamp formatting edge cases

---

### 2. **T-AUTH-02: Interactive Device Auth Flow Test**
   **Status:** ❌ NOT IMPLEMENTED  
   **Specification Lines:** Lines 74, 150-169, 198 in SPECIFICATION.md
   
   **Current State:**
   - Only T-AUTH-01 (auth status check) is implemented in `AuthTestSuite.cs`
   - No interactive device flow test exists
   - Interactive mode exists but only for test selection, not OAuth flow testing
   
   **Required Implementation:**
   
   The specification describes two modes for `--interactive`:
   1. **Mode 1:** Interactive test selection (✅ IMPLEMENTED)
   2. **Mode 2:** Interactive device auth flow testing (❌ MISSING)
   
   **Spec Requirements (Lines 161-169):**
   ```
   Mode 2: Interactive Device Auth Flow Testing (selected via menu or when no automated tests match)
   1. Prompt user to clear existing authentication: `lopen auth logout`
   2. Initiate device flow: `lopen auth login`
   3. Guide user through OAuth device code flow
   4. Prompt for MFA completion
   5. Validate credential storage succeeds
   6. Check for BUG-AUTH-001 (GCM credential store error)
   7. Report success/failure with diagnostic information
   ```
   
   **Implementation Approach:**
   
   **Option A: Add Manual Test Case Type**
   ```csharp
   // Create new test case type: InteractiveTestCase.cs
   public sealed class InteractiveTestCase : ITestCase
   {
       public string TestId { get; }
       public string Description { get; }
       public string Suite { get; }
       
       private readonly Func<TestContext, CancellationToken, Task<TestResult>> _executeFunc;
       
       public async Task<TestResult> ExecuteAsync(TestContext context, CancellationToken ct)
       {
           return await _executeFunc(context, ct);
       }
   }
   
   // In AuthTestSuite.cs - Add T-AUTH-02
   yield return new InteractiveTestCase(
       testId: "T-AUTH-02",
       description: "Interactive device auth flow",
       suite: SuiteName,
       executeFunc: async (context, ct) =>
       {
           // 1. Prompt to logout
           Console.WriteLine("Step 1: Logging out...");
           await RunCommand("lopen", ["auth", "logout"], ct);
           
           // 2. Initiate login
           Console.WriteLine("Step 2: Initiating device flow...");
           Console.WriteLine("Please follow the prompts to complete OAuth authentication.");
           var (output, exitCode) = await RunCommand("lopen", ["auth", "login"], ct);
           
           // 3. Validate credential storage
           Console.WriteLine("Step 3: Validating credentials...");
           var (statusOutput, statusCode) = await RunCommand("lopen", ["auth", "status"], ct);
           
           bool success = statusOutput.Contains("Authenticated", StringComparison.OrdinalIgnoreCase);
           
           return new TestResult
           {
               TestId = "T-AUTH-02",
               Suite = "auth",
               Description = "Interactive device auth flow",
               Status = success ? TestStatus.Pass : TestStatus.Fail,
               Duration = TimeSpan.Zero, // Calculated elsewhere
               StartTime = DateTimeOffset.Now,
               EndTime = DateTimeOffset.Now,
               ResponsePreview = statusOutput,
               Error = success ? null : "Authentication failed or credentials not stored"
           };
       }
   );
   ```
   
   **Option B: Separate Interactive Mode Command**
   ```bash
   # Add dedicated command for manual auth testing
   lopen test self --suite auth --interactive
   
   # Or specific flag
   lopen test self --test-auth-flow
   ```
   
   **Considerations:**
   - Interactive tests cannot run in parallel with automated tests
   - Should prompt user before executing (can't be automated in CI/CD)
   - Needs clear UI guidance (Spectre.Console panels)
   - Should validate BUG-AUTH-001 (GCM credential store error)
   
   **Estimated Effort:** 8-12 hours (includes UI design, error handling, testing)
   
   **Test Changes Needed:**
   - Mock implementation for CI/CD testing
   - Integration test that simulates interactive flow
   - Document manual testing procedure

---

### 3. **Stack Traces Only in Debug/Verbose Mode**
   **Status:** ⚠️ PARTIALLY IMPLEMENTED  
   **Specification:** Implied by "verbose output" requirement
   
   **Current State:**
   - Exception handling in `CommandTestCase.cs` line 99-104:
     ```csharp
     catch (Exception ex)
     {
         stopwatch.Stop();
         return CreateResult(startTime, stopwatch.Elapsed, TestStatus.Error,
             error: ex.Message);  // Only message, not stack trace
     }
     ```
   - Only `ex.Message` is captured, not full stack trace
   - No conditional logic based on verbose mode
   
   **Gap:**
   - Stack traces are never captured or displayed
   - Verbose mode doesn't change error detail level
   
   **Required Changes:**
   
   ```csharp
   // In CommandTestCase.cs - ExecuteAsync method
   catch (Exception ex)
   {
       stopwatch.Stop();
       
       // Capture full error details based on verbose mode
       var errorMessage = context.Verbose 
           ? $"{ex.Message}\n\nStack Trace:\n{ex.StackTrace}"
           : ex.Message;
       
       return CreateResult(startTime, stopwatch.Elapsed, TestStatus.Error,
           error: errorMessage);
   }
   ```
   
   **Alternative Approach:**
   - Add `--debug` flag separate from `--verbose`
   - Store full exception in TestResult (new property: `Exception? Exception`)
   - Conditionally display in TestOutputService based on verbosity level
   
   **Implementation:**
   ```csharp
   // In TestResult.cs - Add new property
   public Exception? Exception { get; init; }
   
   // In TestOutputService.cs - DisplayVerboseResult
   if (result.Status == TestStatus.Error && context.Verbose)
   {
       if (result.Exception is not null)
       {
           _output.Muted($"  Exception: {result.Exception.GetType().Name}");
           _output.Muted($"  Stack Trace:\n{result.Exception.StackTrace}");
       }
   }
   ```
   
   **Estimated Effort:** 2-3 hours
   
   **Test Changes Needed:**
   - Add test for verbose error output with stack trace
   - Add test for non-verbose error output without stack trace

---

### Additional Observations

#### 🎯 Architecture Strengths
1. Clean separation of concerns (TestRunner, TestCase, Validator, OutputService)
2. Good use of Command Pattern for test cases
3. Strategy Pattern for validators (extensible)
4. Proper async/await usage throughout
5. Comprehensive test coverage (60+ tests)

#### 🔧 Minor Improvements (Not in Spec)
1. **Rate Limiting**: Specification mentions rate limiting (line 227) but not implemented
   - Could use `SemaphoreSlim` to throttle concurrent test execution
   
2. **Test Logs Directory**: Spec mentions "temporary directory" for logs (line 186)
   - Currently not creating or saving per-test logs to files
   
3. **REPL Tests**: Specification includes REPL test suite (T-REPL-01, T-REPL-02, T-REPL-03)
   - Not implemented (would require process input/output simulation)
   
4. **Advanced Validation**: Research doc mentions regex and fuzzy matching
   - Only keyword validation implemented (sufficient for Phase 1)

#### 📊 Test Coverage Breakdown
- ✅ KeywordValidator: 13 tests
- ✅ TestRunner: 6 tests  
- ✅ TestOutputService: 7 tests
- ✅ TestContext: 2 tests
- ✅ TestResult: 3 tests
- ✅ TestRunSummary: 8 tests
- ✅ TestSuiteRegistry: 6 tests
- ✅ InteractiveTestSelector: 15 tests
- **Total:** 60+ tests (good coverage)

---

### Priority Recommendations

#### Priority 1: Per-Test Logging with Timestamps ⭐⭐⭐
- **Effort:** Low (2-4 hours)
- **Value:** High (completes acceptance criteria)
- **Risk:** Low (simple string formatting)
- **Action:** Modify `TestOutputService.DisplayVerboseResult()` to include timestamp prefix

#### Priority 2: Stack Traces in Verbose Mode ⭐⭐
- **Effort:** Low (2-3 hours)
- **Value:** Medium (improves debugging experience)
- **Risk:** Low (conditional display logic)
- **Action:** Enhance exception handling in `CommandTestCase` and `TestOutputService`

#### Priority 3: T-AUTH-02 Interactive Device Flow Test ⭐
- **Effort:** High (8-12 hours)
- **Value:** Medium (completes auth test suite, helps catch BUG-AUTH-001)
- **Risk:** Medium (requires careful UX design, manual testing)
- **Action:** Create `InteractiveTestCase` type and add to `AuthTestSuite`
- **Note:** This is marked as "manual" in spec and may be intentionally deferred

---

### Updated Implementation Roadmap

```diff
### Phase 1: Core Infrastructure (Week 1)
- [x] Implement `ITestCase` interface and base classes
- [x] Implement `TestRunner` with parallel execution
- [x] Implement `CliWrapProcessExecutor` (used System.Diagnostics.Process instead)
- [x] Implement basic `KeywordValidator`
- [x] Add Spectre.Console progress display

### Phase 2: Test Definitions (Week 2)
- [x] Define embedded test suites for chat, session, auth
- [x] Implement test case factory (TestSuiteRegistry)
- [x] Add test filtering logic
- [x] Implement interactive mode with Spectre.Console
- [ ] Add per-test logging with timestamps ⬅️ MISSING
- [ ] Add T-AUTH-02 interactive device flow test ⬅️ MISSING

### Phase 3: Advanced Features (Week 3)
- [x] Add timeout and cancellation support
- [x] Implement JSON output format
- [ ] Implement Polly resilience policies (deferred)
- [ ] Add structured logging with Serilog (deferred)
- [ ] Stack traces in verbose/debug mode ⬅️ PARTIALLY IMPLEMENTED

### Phase 4: Extensibility (Week 4)
- [ ] Add YAML test definition loader (deferred)
- [ ] Implement JSON schema validation (deferred)
- [ ] Add custom validator plugin support (deferred)
- [ ] Create user documentation (deferred)

### Phase 5: Testing & Polish (Week 5)
- [x] Unit tests for test harness (xUnit)
- [ ] Integration tests with real lopen commands (partial)
- [ ] CI/CD pipeline integration
- [ ] Performance optimization
```

---

### Conclusion

The Testing module implementation is **~85% complete** relative to the specification acceptance criteria. The core functionality is solid and production-ready. The remaining work consists of:

1. **Minor enhancement:** Add timestamps to verbose logging (2-4 hours)
2. **Minor enhancement:** Improve stack trace display in verbose mode (2-3 hours)  
3. **Optional manual test:** T-AUTH-02 interactive device flow (8-12 hours, may be intentionally deferred)

**Recommendation:** Mark the two high-priority acceptance criteria as implemented:
- `- [x] Per-test logging with timestamps` (after 2-hour fix)
- `- [x] Stack traces only in debug/verbose mode` (after 2-hour fix)

Leave T-AUTH-02 as Phase 2 / manual testing, as it requires human interaction and cannot be fully automated.

---

**Gap Analysis Complete - January 26, 2025**

