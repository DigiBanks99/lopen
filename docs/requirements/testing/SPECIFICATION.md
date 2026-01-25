# Self-Testing - Specification

> Automated self-testing command to verify lopen functionality

## Requirements

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| REQ-020 | Self-Testing Command | High | 🔵 Planned |

---

## REQ-020: Self-Testing Command

### Description
Implement a `lopen test self` command that runs automated tests against the lopen CLI itself. Tests execute real commands (chat, repl, session, etc.) with simple prompts and validate responses contain expected keywords/patterns. Uses gpt-5-mini by default to avoid depleting premium request quotas.

### Prerequisites
- Authenticated with GitHub (valid token)
- Copilot CLI available in PATH
- Internet connectivity for API requests

### Command Signature
```bash
lopen test self                           # Run all test suites
lopen test self --verbose                 # Show detailed output per test
lopen test self --filter <pattern>        # Run tests matching pattern
lopen test self --interactive             # Interactive suite/test selection
lopen test self --timeout <seconds>       # Override default timeout (30s)
lopen test self --model <model>           # Override model (default: gpt-5-mini)
lopen test self --format json             # Output results as JSON
```

### Acceptance Criteria
- [ ] Tests all key commands: chat, repl, session list/delete
- [ ] Uses gpt-5-mini by default to minimize costs
- [ ] Validates responses contain expected keywords/patterns
- [ ] Parallel test execution with aggregated results
- [ ] Per-test logging with timestamps
- [ ] Exit code 0 if all pass, 1 if any fail
- [ ] Rich terminal output using Spectre.Console (tables, progress bars, panels)
- [ ] Interactive mode for suite/test selection
- [ ] Filter tests by name pattern
- [ ] Configurable timeout per test
- [ ] Model override via --model flag
- [ ] JSON output format for CI/CD integration

### Test Suites

#### Suite 1: Chat Command
| Test ID | Description | Input | Expected Pattern |
|---------|-------------|-------|------------------|
| T-CHAT-01 | Basic question | "What is 2+2?" | "4", "four", "equals" |
| T-CHAT-02 | Code generation | "Write hello world in Dotnet" | "print", "hello" |
| T-CHAT-03 | Error handling | Invalid command syntax | Error message |

#### Suite 2: REPL Command
| Test ID | Description | Input | Expected Pattern |
|---------|-------------|-------|------------------|
| T-REPL-01 | Single prompt | "exit" after greeting | REPL exits cleanly |
| T-REPL-02 | Multi-turn | Two simple prompts + exit | Both responses received |
| T-REPL-03 | History navigation | Up arrow after command | Command recalled |

#### Suite 3: Session Management
| Test ID | Description | Command | Expected Pattern |
|---------|-------------|---------|------------------|
| T-SESSION-01 | List sessions | `lopen session list` | Table output or "No sessions" |
| T-SESSION-02 | Delete session | Create + delete session | Success message |

#### Suite 4: Authentication
| Test ID | Description | Command | Expected Pattern |
|---------|-------------|---------|------------------|
| T-AUTH-01 | Check status | `lopen auth status` | "authenticated" or token info |

### Validation Strategy

**Keyword Matching**: Each test defines expected keywords/patterns. Response passes if it contains **any** of the expected patterns (case-insensitive).

Example:
```json
{
  "input": "What is 2+2?",
  "expected_patterns": ["4", "four", "equals"],
  "match_mode": "any"
}
```

### Output Format

#### Default (Terminal UI with Spectre.Console)
```
╭─────────────────────────────────────────╮
│      Lopen Self-Test Suite             │
│      Model: gpt-5-mini                  │
╰─────────────────────────────────────────╯

Running 11 tests across 4 suites...

┌──────────────┬────────────┬──────────┬──────────┐
│ Test ID      │ Status     │ Duration │ Details  │
├──────────────┼────────────┼──────────┼──────────┤
│ T-CHAT-01    │ ✓ PASS     │ 1.2s     │          │
│ T-CHAT-02    │ ✓ PASS     │ 2.1s     │          │
│ T-CHAT-03    │ ✗ FAIL     │ 0.5s     │ Expected │
│              │            │          │ keyword  │
│ T-REPL-01    │ ✓ PASS     │ 1.8s     │          │
└──────────────┴────────────┴──────────┴──────────┘

╭─────────────────────────────────────────╮
│ Summary                                 │
│ ✓ Passed:  10/11                        │
│ ✗ Failed:  1/11                         │
│ ⏱ Total:   18.5s                        │
╰─────────────────────────────────────────╯
```

#### JSON Format (`--format json`)
```json
{
  "summary": {
    "total": 11,
    "passed": 10,
    "failed": 1,
    "duration_seconds": 18.5,
    "model": "gpt-5-mini"
  },
  "results": [
    {
      "test_id": "T-CHAT-01",
      "suite": "chat",
      "description": "Basic question",
      "status": "pass",
      "duration_seconds": 1.2,
      "input": "What is 2+2?",
      "response_preview": "The answer is 4.",
      "matched_pattern": "4"
    },
    {
      "test_id": "T-CHAT-03",
      "suite": "chat",
      "status": "fail",
      "duration_seconds": 0.5,
      "error": "No expected patterns found in response"
    }
  ]
}
```

### Interactive Mode

When `--interactive` flag is used:
1. Display available test suites with descriptions
2. Prompt user to select suites (multi-select with checkboxes)
3. For each selected suite, show tests and allow selection
4. Ask the user to confirm the model
5. Execute selected tests with confirmation

### Filter Mode

Pattern matching for `--filter`:
- Matches against test ID, suite name, or description
- Case-insensitive substring matching
- Examples:
  - `--filter chat` → runs all chat suite tests
  - `--filter T-REPL` → runs all REPL tests
  - `--filter "basic"` → runs tests with "basic" in description

### Parallel Execution

- Tests run concurrently (up to 4 parallel tasks by default)
- Each test isolated with its own Copilot session
- Aggregated results displayed after all tests complete
- Individual test logs saved to temporary directory (shown with --verbose)

### Test Cases
| ID | Description | Expected | Status |
|----|-------------|----------|--------|
| TC-020-01 | `lopen test self` | Runs all tests, exits 0 if pass | ⏳ |
| TC-020-02 | `lopen test self --filter chat` | Runs only chat tests | ⏳ |
| TC-020-03 | `lopen test self --verbose` | Shows detailed logs | ⏳ |
| TC-020-04 | `lopen test self --format json` | Valid JSON output | ⏳ |
| TC-020-05 | `lopen test self --model gpt-5` | Uses specified model | ⏳ |
| TC-020-06 | Test failure | Exits with code 1 | ⏳ |
| TC-020-07 | `lopen test self --interactive` | Prompts for selection | ⏳ |
| TC-020-08 | `lopen test self --timeout 60` | Uses custom timeout | ⏳ |

---

## Implementation Notes

### Model Selection Priority
1. `--model` flag (if provided)
2. Environment variable `LOPEN_TEST_MODEL` (if set)
3. Default: `gpt-5-mini` (hard-coded)

### Error Handling
- **Timeout**: Test fails if exceeds timeout (default 30s)
- **API Error**: Test fails with error message logged
- **Auth Error**: Abort entire run with clear message
- **Command Not Found**: Test fails with error details

### Test Isolation
- Each test creates a new Copilot session
- Sessions cleaned up after test completion
- Tests do not interfere with user's existing sessions

### Extensibility
- Test definitions stored in embedded resources (C# code)
- Future: External test case files (JSON/YAML) for user extensibility
- Future: Plugin system for custom test validators

### Performance Considerations
- Default: 4 parallel tests (configurable via environment variable)
- Rate limiting to avoid API throttling
- Early abort on critical failures (e.g., auth failure)

### Spectre.Console Components
- **Progress Bar**: Show test execution progress
- **Table**: Display results summary
- **Panel**: Group related information (header, summary)
- **Tree**: Show suite hierarchy in interactive mode
- **Status**: Spinner during test execution
- **Rule**: Visual separators between sections
