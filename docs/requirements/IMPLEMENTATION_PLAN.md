# Implementation Plan

> Next priority: **JTBD-001** - Initialize .NET 10 solution with project structure

## Objective

Create the foundational .NET 10 solution structure with CLI entry point, core library, and test projects.

---

## Steps

### 1. Create Solution Structure

```
lopen/
├── src/
│   ├── Lopen.Cli/        # CLI entry point
│   └── Lopen.Core/       # Core business logic
├── tests/
│   ├── Lopen.Cli.Tests/
│   └── Lopen.Core.Tests/
├── Directory.Build.props  # Shared build settings
└── Lopen.sln
```

**Commands:** See [cli-core/RESEARCH.md](cli-core/RESEARCH.md#1-net-10-solution-structure)

### 2. Add NuGet Dependencies

| Project | Package | Purpose |
|---------|---------|---------|
| Lopen.Cli | System.CommandLine | CLI framework |
| Lopen.Cli | Spectre.Console | TUI output |
| Lopen.Core | (none initially) | Pure .NET |
| Tests | FluentAssertions | Test assertions |
| Tests | coverlet.collector | Code coverage |

**Commands:** See [cli-core/RESEARCH.md](cli-core/RESEARCH.md#2-systemcommandline-setup)

### 3. Implement Minimal CLI

Create `Program.cs` with:
- RootCommand with description
- Version display (auto-provided by System.CommandLine)
- Help display (auto-provided)
- Parse and invoke pattern

**Code:** See [cli-core/RESEARCH.md](cli-core/RESEARCH.md#root-command-with-version-and-help)

### 4. Create VersionService in Core

```csharp
public interface IVersionService
{
    string GetVersion();
    string GetFormattedVersion(bool json);
}
```

- Read version from assembly metadata
- Support plain text and JSON output

### 5. Add Unit Tests

- Test VersionService returns valid semver
- Test JSON output format

**Example:** See [cli-core/RESEARCH.md](cli-core/RESEARCH.md#4-xunit-with-fluentassertions)

### 6. Configure Publishing

Add single-file publishing settings to Lopen.Cli.csproj.

**Config:** See [cli-core/RESEARCH.md](cli-core/RESEARCH.md#5-single-file-publishing)

---

## Acceptance Criteria

- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes
- [ ] `dotnet run --project src/Lopen.Cli -- --version` displays version
- [ ] `dotnet run --project src/Lopen.Cli -- --help` displays help

---

## Estimated Effort

~2-4 hours

---

## Next Steps After Completion

1. **JTBD-002**: REQ-001 Version command with `--format json`
2. **JTBD-003**: REQ-002 Help command enhancements
