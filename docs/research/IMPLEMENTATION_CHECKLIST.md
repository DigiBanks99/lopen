# Lopen Self-Testing Module - Implementation Checklist

Based on comprehensive research (see [TESTING_MODULE_RESEARCH.md](./TESTING_MODULE_RESEARCH.md))

---

## Phase 1: Core Infrastructure (Week 1)

### Interfaces & Models
- [ ] Create `ITestCase` interface
- [ ] Create `TestContext` class
- [ ] Create `TestResult` record
- [ ] Create `TestStatus` enum
- [ ] Create `IProcessExecutor` interface
- [ ] Create `ProcessResult` record
- [ ] Create `IResponseValidator` interface
- [ ] Create `ValidationResult` record

### Core Implementations
- [ ] Implement `TestRunner` class
  - [ ] Parallel execution with `Parallel.ForEachAsync`
  - [ ] Progress tracking with Spectre.Console
  - [ ] Result aggregation
- [ ] Implement `CliWrapProcessExecutor`
  - [ ] Command execution
  - [ ] Timeout support
  - [ ] Output capture
- [ ] Implement `KeywordValidator`
  - [ ] Case-insensitive substring matching
  - [ ] Multiple pattern support
- [ ] Implement `TestRunSummary` class
  - [ ] Statistics calculation
  - [ ] Duration tracking

### Dependencies
- [ ] Add `CliWrap` package
- [ ] Add `Spectre.Console` package (already exists)
- [ ] Add `System.CommandLine` package (already exists)

---

## Phase 2: Test Definitions (Week 2)

### Embedded Test Suites
- [ ] Create `TestDefinitions` static class
- [ ] Define chat command tests (T-CHAT-01, T-CHAT-02, T-CHAT-03)
- [ ] Define REPL tests (T-REPL-01, T-REPL-02, T-REPL-03)
- [ ] Define session tests (T-SESSION-01, T-SESSION-02)
- [ ] Define auth tests (T-AUTH-01)

### Test Infrastructure
- [ ] Create `TestCaseFactory`
- [ ] Implement `ChatCommandTestCase`
- [ ] Implement `SessionCommandTestCase`
- [ ] Implement `AuthCommandTestCase`

### CLI Command
- [ ] Create `TestSelfCommand` class
- [ ] Add `--verbose` flag
- [ ] Add `--filter` flag
- [ ] Add `--timeout` flag
- [ ] Add `--model` flag
- [ ] Add `--format` flag
- [ ] Add `--interactive` flag

### Filtering & Selection
- [ ] Implement test filtering logic
- [ ] Implement interactive suite selection
- [ ] Implement interactive test selection

---

## Phase 3: Advanced Features (Week 3)

### Timeout & Cancellation
- [ ] Integrate `CancellationToken` throughout
- [ ] Implement per-test timeout
- [ ] Handle `OperationCanceledException`
- [ ] Test timeout error messages

### Resilience (Polly)
- [ ] Add Polly packages
- [ ] Create `ResilientProcessExecutor`
- [ ] Configure retry policy
- [ ] Configure timeout policy
- [ ] Handle transient failures

### Logging
- [ ] Add Serilog packages
- [ ] Create `TestLogging` helper
- [ ] Log test start/completion
- [ ] Log command execution
- [ ] Save per-test logs to temp directory

### JSON Output
- [ ] Create `JsonTestReporter`
- [ ] Implement JSON serialization
- [ ] Support `--format json` flag
- [ ] Validate JSON output format

### Spectre.Console Enhancements
- [ ] Results table with status icons
- [ ] Summary panel with statistics
- [ ] Progress bar with spinners
- [ ] Color-coded output

---

## Phase 4: Extensibility (Week 4)

### Configuration
- [ ] Create `TestConfiguration` class
- [ ] Load from config file (`~/.lopen/test-config.json`)
- [ ] Load from environment variables
- [ ] Configuration priority implementation

### External Tests (YAML)
- [ ] Add YamlDotNet package
- [ ] Create JSON schema for test definitions
- [ ] Implement `ExternalTestLoader`
- [ ] Parse YAML test files
- [ ] Validate against schema

### Plugin System
- [ ] Create `ITestCasePlugin` interface
- [ ] Implement plugin discovery
- [ ] Load custom validators
- [ ] Load custom test cases

### Documentation
- [ ] User guide for running tests
- [ ] Guide for writing custom tests
- [ ] Guide for writing plugins
- [ ] API documentation

---

## Phase 5: Testing & Polish (Week 5)

### Unit Tests (xUnit)
- [ ] Test `TestRunner` logic
- [ ] Test `KeywordValidator`
- [ ] Test `CliWrapProcessExecutor` (with mocks)
- [ ] Test filtering logic
- [ ] Test interactive selection
- [ ] Test JSON reporter
- [ ] Test configuration loading
- [ ] Achieve 80%+ code coverage

### Integration Tests
- [ ] Test real `lopen chat` execution
- [ ] Test real `lopen session` execution
- [ ] Test real `lopen auth` execution
- [ ] Test timeout handling
- [ ] Test error scenarios
- [ ] Test parallel execution
- [ ] Test test isolation

### CI/CD
- [ ] Create GitHub Actions workflow
- [ ] Authenticate with GitHub Copilot in CI
- [ ] Run self-tests on PR
- [ ] Upload test results as artifacts
- [ ] Fail pipeline on test failures

### Performance
- [ ] Profile test execution time
- [ ] Optimize parallel execution
- [ ] Memory usage profiling
- [ ] Rate limiting verification

### Polish
- [ ] Code review
- [ ] Documentation review
- [ ] Error message review
- [ ] UI/UX refinement

---

## Acceptance Criteria

From [SPECIFICATION.md](../requirements/testing/SPECIFICATION.md):

- [ ] Tests all key commands: chat, repl, session list/delete
- [ ] Uses gpt-5-mini by default to minimize costs
- [ ] Validates responses contain expected keywords/patterns
- [ ] Parallel test execution with aggregated results
- [ ] Per-test logging with timestamps
- [ ] Exit code 0 if all pass, 1 if any fail
- [ ] Rich terminal output using Spectre.Console
- [ ] Interactive mode for suite/test selection
- [ ] Filter tests by name pattern
- [ ] Configurable timeout per test
- [ ] Model override via --model flag
- [ ] JSON output format for CI/CD integration

---

## Quick Commands

```bash
# Create feature branch
git checkout -b feature/self-testing

# Run after implementation
lopen test self
lopen test self --verbose
lopen test self --filter chat
lopen test self --interactive
lopen test self --timeout 60
lopen test self --model gpt-5
lopen test self --format json

# Run test harness tests
dotnet test tests/Lopen.Commands.Testing.Tests
```

---

## Success Metrics

- [ ] All 11 built-in tests pass
- [ ] Average test execution < 2 seconds
- [ ] Total suite time < 5 minutes
- [ ] Memory usage < 100MB
- [ ] Code coverage > 80%
- [ ] Zero flaky tests (retry logic handles variance)

---

**Status:** Ready for Implementation  
**Estimated Time:** 5 weeks  
**Priority:** High (REQ-020)

See [TESTING_MODULE_RESEARCH.md](./TESTING_MODULE_RESEARCH.md) for detailed technical guidance.
