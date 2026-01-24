# CLI Core - Specification

> Foundational CLI commands: version and help

## Requirements

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| REQ-001 | Version Command | High | 🟢 Complete |
| REQ-002 | Help/Commands List | High | 🔴 Not Started |

---

## REQ-001: Version Command

### Description
Display the current version of the Lopen CLI application.

### Command Signature
```bash
lopen version
lopen version --format json
lopen version -f json
lopen --version  # Built-in System.CommandLine
```

### Acceptance Criteria
- [x] Displays semantic version (e.g., `0.1.0`)
- [x] Exits with code 0 on success
- [x] Output format: `lopen version X.Y.Z`
- [x] JSON format: `{"version": "X.Y.Z"}` with `--format json`
- [x] Short flag `-f` for format option

### Test Cases
| ID | Description | Expected | Status |
|----|-------------|----------|--------|
| TC-001-01 | `lopen version` | Outputs `lopen version X.Y.Z` | ✅ |
| TC-001-02 | `lopen version --format json` | Outputs valid JSON | ✅ |
| TC-001-03 | `lopen version -f json` | Same as `--format json` | ✅ |
| TC-001-04 | `lopen --version` | Built-in version output | ✅ |

---

## REQ-002: Help/Commands List

### Description
Display available commands and their descriptions.

### Command Signature
```bash
lopen --help
lopen -h
lopen help
lopen help <command>
```

### Acceptance Criteria
- [ ] Lists all available commands with descriptions
- [ ] Supports `--help`, `-h`, and `help` subcommand
- [ ] Provides detailed help for specific commands via `help <command>`
- [ ] Output is formatted for terminal readability
- [ ] Supports JSON output format via `--format json`

### Test Cases
| ID | Description | Expected |
|----|-------------|----------|
| TC-002-01 | `lopen --help` | Lists all commands |
| TC-002-02 | `lopen help auth` | Shows auth subcommand details |
| TC-002-03 | `lopen --help --format json` | Outputs valid JSON |

---

## Implementation Notes

### CLI Framework
- Use `System.CommandLine` for command parsing
- Subcommand pattern: `lopen <command> <subcommand> [options]`
- Argument style: POSIX (`--flag`, `-f`)

### Output Formats
- Plain text (default)
- JSON (`--format json`)
