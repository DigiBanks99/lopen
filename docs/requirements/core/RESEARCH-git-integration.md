# Research: Git Integration for Lopen CLI Orchestrator

> **Module:** Core  
> **Date:** 2025-07-15  
> **Status:** Complete

## Requirements Recap

The core orchestrator needs Git operations for:

1. **Auto-commit** after task completion with conventional commit messages
2. **Branch creation** per module (e.g., `lopen/auth`, `lopen/storage`)
3. **Revert** to last known-good commit
4. **File modification timestamps** for change detection
5. **Reading diffs** for oracle verification

---

## Approach 1: LibGit2Sharp (Managed Git)

### Maintenance Status

| Metric | Value |
|--------|-------|
| Latest release | **v0.31.0** (December 3, 2024) |
| Target frameworks | `net8.0`, `net472` |
| Underlying native lib | libgit2 v1.8.4 |
| NuGet downloads | ~4.2M (v0.31.0 alone) |
| GitHub dependents | 93 repos (GitExtensions, ABP Framework, Stryker.NET, etc.) |
| License | MIT |
| Active CI | Yes — GitHub Actions badge is green |

**Assessment:** Actively maintained. Dropped .NET 6 (EOL) in v0.31.0 and targets .NET 8 directly. Release cadence is roughly every 6–12 months. The project has survived multiple maintainer transitions and remains the de-facto .NET Git library.

### Native Library Dependency

LibGit2Sharp depends on `LibGit2Sharp.NativeBinaries` (v2.0.323), which bundles platform-specific native binaries for:

- Windows (x86, x64, arm64)
- Linux (x64, arm64, ppc64le)
- macOS (x64, arm64)

**Concern:** The native binaries add ~15–25 MB to deployment size. They are bundled as NuGet runtime assets and copied to output automatically. This works well for self-contained deployments but can cause issues in:

- Trimmed/AOT-published apps (requires configuration)
- Docker Alpine images (musl vs glibc — needs `libgit2` compiled for musl or use Debian-based images)
- CI environments without native library support

### API Examples

#### 1. Auto-Commit with Conventional Message

```csharp
using LibGit2Sharp;

public static class GitCommitter
{
    public static Commit AutoCommit(string repoPath, string taskName, string module)
    {
        using var repo = new Repository(repoPath);

        // Stage all changes
        Commands.Stage(repo, "*");

        // Check if there are staged changes
        var status = repo.RetrieveStatus();
        if (!status.IsDirty)
            return repo.Head.Tip;

        // Conventional commit message
        var message = $"feat({module}): complete task '{taskName}'";

        var author = new Signature("Lopen", "lopen@automated", DateTimeOffset.Now);
        return repo.Commit(message, author, author);
    }
}
```

#### 2. Branch Creation Per Module

```csharp
public static Branch CreateModuleBranch(string repoPath, string module)
{
    using var repo = new Repository(repoPath);

    var branchName = $"lopen/{module}";

    // Check if branch already exists
    var existing = repo.Branches[branchName];
    if (existing is not null)
    {
        Commands.Checkout(repo, existing);
        return existing;
    }

    // Create from current HEAD and check out
    var branch = repo.CreateBranch(branchName);
    Commands.Checkout(repo, branch);
    return branch;
}
```

#### 3. Revert to Last Known-Good Commit

```csharp
public static void RevertToCommit(string repoPath, string commitSha)
{
    using var repo = new Repository(repoPath);
    var target = repo.Lookup<Commit>(commitSha);

    // Hard reset — resets index and working directory
    repo.Reset(ResetMode.Hard, target);
}

public static void RevertLastTask(string repoPath)
{
    using var repo = new Repository(repoPath);

    // Go back one commit (the last auto-commit)
    var previous = repo.Head.Tip.Parents.First();
    repo.Reset(ResetMode.Hard, previous);
}
```

#### 4. Check File Modification Timestamps

```csharp
public static DateTimeOffset? GetLastModified(string repoPath, string filePath)
{
    using var repo = new Repository(repoPath);

    // Query commit history for the file
    var log = repo.Commits.QueryBy(filePath);
    var latest = log.FirstOrDefault();

    return latest?.Commit.Author.When;
}

// For working-directory timestamps (filesystem-level)
public static DateTime GetFileSystemTimestamp(string repoPath, string filePath)
{
    var fullPath = Path.Combine(repoPath, filePath);
    return File.GetLastWriteTimeUtc(fullPath);
}
```

#### 5. Reading Diffs for Oracle Verification

```csharp
public static string GetWorkingDirectoryDiff(string repoPath)
{
    using var repo = new Repository(repoPath);

    // Compare HEAD tree to working directory + index
    var changes = repo.Diff.Compare<Patch>(
        repo.Head.Tip.Tree,
        DiffTargets.Index | DiffTargets.WorkingDirectory);

    return changes.Content; // Full unified diff as string
}

public static string GetCommitDiff(string repoPath, string commitSha)
{
    using var repo = new Repository(repoPath);
    var commit = repo.Lookup<Commit>(commitSha);
    var parent = commit.Parents.FirstOrDefault();

    var changes = repo.Diff.Compare<Patch>(
        parent?.Tree,
        commit.Tree);

    return changes.Content;
}

// Structured diff data (file-level changes)
public static IEnumerable<(string Path, ChangeKind Status, int Added, int Removed)>
    GetDiffSummary(string repoPath, string commitSha)
{
    using var repo = new Repository(repoPath);
    var commit = repo.Lookup<Commit>(commitSha);
    var parent = commit.Parents.FirstOrDefault();

    var changes = repo.Diff.Compare<Patch>(parent?.Tree, commit.Tree);

    foreach (var entry in changes)
    {
        yield return (entry.Path, entry.Status, entry.LinesAdded, entry.LinesDeleted);
    }
}
```

### Known Issues & Limitations

1. **No rebase support** — LibGit2Sharp does not expose `git rebase`. Not needed for Lopen's use case.
2. **Limited `git worktree` support** — Worktree add was buggy (fixed in v0.31.0).
3. **No sparse checkout** — Not applicable to Lopen.
4. **`IDisposable` on `Repository`** — Must ensure proper disposal or risk file lock leaks on Windows.
5. **File locking on Windows** — The native library holds file handles; concurrent access from `git` CLI and LibGit2Sharp simultaneously can cause `AccessDeniedException`.
6. **No GPG signing** — Commit signing is not supported natively. Would require shelling out to `gpg`.
7. **Alpine Linux** — Native binaries target glibc. Alpine uses musl. Requires Debian-based Docker images or manual native lib compilation.

---

## Approach 2: Process-Based Git (System.Diagnostics.Process)

### Wrapper Pattern

```csharp
using System.Diagnostics;
using System.Text;

public sealed class GitCli
{
    private readonly string _workingDirectory;

    public GitCli(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }

    private async Task<GitResult> RunAsync(string arguments, CancellationToken ct = default)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            // Ensure consistent output regardless of user's locale
            Environment = { ["LC_ALL"] = "C", ["GIT_TERMINAL_PROMPT"] = "0" }
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        return new GitResult(process.ExitCode, stdout.ToString().Trim(), stderr.ToString().Trim());
    }

    /// <summary>
    /// Runs a git command; throws on non-zero exit code.
    /// </summary>
    private async Task<string> ExecAsync(string arguments, CancellationToken ct = default)
    {
        var result = await RunAsync(arguments, ct);
        if (result.ExitCode != 0)
            throw new GitException(result.ExitCode, result.StdErr, arguments);
        return result.StdOut;
    }
}

public readonly record struct GitResult(int ExitCode, string StdOut, string StdErr);

public sealed class GitException(int exitCode, string error, string command)
    : Exception($"git {command} failed (exit {exitCode}): {error}")
{
    public int ExitCode { get; } = exitCode;
    public string GitError { get; } = error;
    public string Command { get; } = command;
}
```

### API Examples

#### 1. Auto-Commit with Conventional Message

```csharp
public async Task<string> CommitAllAsync(string module, string taskName, CancellationToken ct = default)
{
    await ExecAsync("add -A", ct);

    // Check if there's anything to commit
    var status = await RunAsync("status --porcelain", ct);
    if (string.IsNullOrWhiteSpace(status.StdOut))
        return await ExecAsync("rev-parse HEAD", ct);

    var message = $"feat({module}): complete task '{taskName}'";
    await ExecAsync($"""commit -m "{message}" --author="Lopen <lopen@automated>" """, ct);
    return await ExecAsync("rev-parse HEAD", ct);
}
```

#### 2. Branch Creation Per Module

```csharp
public async Task CreateModuleBranchAsync(string module, CancellationToken ct = default)
{
    var branchName = $"lopen/{module}";

    // Check if branch exists
    var result = await RunAsync($"rev-parse --verify {branchName}", ct);
    if (result.ExitCode == 0)
    {
        await ExecAsync($"checkout {branchName}", ct);
        return;
    }

    await ExecAsync($"checkout -b {branchName}", ct);
}
```

#### 3. Revert to Last Known-Good Commit

```csharp
public async Task RevertToCommitAsync(string commitSha, CancellationToken ct = default)
{
    await ExecAsync($"reset --hard {commitSha}", ct);
}

public async Task RevertLastTaskAsync(CancellationToken ct = default)
{
    await ExecAsync("reset --hard HEAD~1", ct);
}
```

#### 4. Check File Modification Timestamps

```csharp
public async Task<DateTimeOffset?> GetLastCommitDateAsync(string filePath, CancellationToken ct = default)
{
    var result = await RunAsync($"log -1 --format=%aI -- \"{filePath}\"", ct);
    if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
        return null;

    return DateTimeOffset.Parse(result.StdOut);
}
```

#### 5. Reading Diffs for Oracle Verification

```csharp
public async Task<string> GetWorkingDiffAsync(CancellationToken ct = default)
{
    // Staged + unstaged against HEAD
    return await ExecAsync("diff HEAD", ct);
}

public async Task<string> GetCommitDiffAsync(string commitSha, CancellationToken ct = default)
{
    return await ExecAsync($"diff {commitSha}~1 {commitSha}", ct);
}

public async Task<string> GetDiffStatAsync(string commitSha, CancellationToken ct = default)
{
    return await ExecAsync($"diff --stat {commitSha}~1 {commitSha}", ct);
}

// Structured diff parsing
public async Task<IReadOnlyList<FileDiff>> GetDiffSummaryAsync(
    string commitSha, CancellationToken ct = default)
{
    var raw = await ExecAsync($"diff --numstat {commitSha}~1 {commitSha}", ct);
    return raw.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line =>
        {
            var parts = line.Split('\t');
            return new FileDiff(
                Path: parts[2],
                Added: int.TryParse(parts[0], out var a) ? a : 0,
                Removed: int.TryParse(parts[1], out var r) ? r : 0);
        }).ToList();
}

public readonly record struct FileDiff(string Path, int Added, int Removed);
```

### Error Handling Patterns

```csharp
// Retry pattern for transient failures (e.g., lock contention)
public async Task<string> ExecWithRetryAsync(
    string arguments, int maxRetries = 3, CancellationToken ct = default)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await ExecAsync(arguments, ct);
        }
        catch (GitException ex) when (
            ex.GitError.Contains("index.lock") && i < maxRetries - 1)
        {
            await Task.Delay(100 * (i + 1), ct);
        }
    }

    throw new InvalidOperationException("Unreachable");
}

// Validate git is available at startup
public async Task<string> ValidateGitAsync(CancellationToken ct = default)
{
    try
    {
        var version = await ExecAsync("--version", ct);
        // e.g., "git version 2.43.0"
        return version;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        throw new InvalidOperationException(
            "Git CLI is not available. Ensure 'git' is installed and on PATH.", ex);
    }
}
```

---

## Comparison

| Criterion | LibGit2Sharp | Process-based `git` CLI |
|---|---|---|
| **NuGet dependency** | `LibGit2Sharp` + `LibGit2Sharp.NativeBinaries` (~15–25 MB native blobs) | None (system `git` required) |
| **Async support** | ❌ Synchronous only (thread pool needed) | ✅ Native `async/await` via `Process.WaitForExitAsync` |
| **API surface** | Strongly typed C# objects (`Commit`, `Branch`, `Patch`) | Raw string output; parsing required |
| **Error handling** | Typed exceptions (`NotFoundException`, `MergeConflictException`) | Exit codes + stderr string matching |
| **Testability** | Can mock `IRepository` (but it's complex) | Easy to mock; wrap behind `IGitCli` interface |
| **Feature coverage** | Subset of git (no rebase, no sparse checkout, no GPG signing) | Full `git` feature set — whatever the CLI supports |
| **Deployment size** | +15–25 MB for native binaries | 0 MB (git must be pre-installed) |
| **Docker / CI** | Needs glibc-based image; native binaries must match platform | Just needs `git` package installed — works everywhere |
| **Concurrent access** | File lock issues on Windows with mixed CLI/lib usage | No lock conflicts (git CLI handles its own locking) |
| **Performance** | Faster for bulk operations (in-process, no fork/exec overhead) | Fork/exec per command; fine for infrequent operations |
| **Maintenance risk** | Dependent on libgit2 native library updates | Zero — `git` CLI is the canonical implementation |
| **.NET 8 compatibility** | ✅ v0.31.0 targets `net8.0` directly | ✅ `System.Diagnostics.Process` has always worked |
| **AOT / Trimming** | ⚠️ Requires trimming configuration for native interop | ✅ No special configuration needed |

---

## Recommendation: Process-Based `git` CLI

**Use the process-based approach** for Lopen. Rationale:

### 1. Lopen Already Requires Git Installed

The core specification explicitly requires `git` as a system dependency. Users must have `git` installed for VCS operations. This eliminates the "git must be pre-installed" concern entirely — it's already a prerequisite.

### 2. Async-First Architecture

Lopen's orchestrator is heavily async (LLM calls, file I/O, tool execution). LibGit2Sharp is synchronous-only, requiring `Task.Run` wrappers that waste thread pool threads. The process-based approach provides native `async/await` via `Process.WaitForExitAsync`.

### 3. Zero Native Dependency Risk

LibGit2Sharp's native binaries add complexity for Docker images (Alpine vs Debian), CI pipelines, and AOT publishing. The CLI approach has none of these concerns.

### 4. Operations Are Infrequent

Lopen runs git operations at task boundaries (after each task completion), not in tight loops. The fork/exec overhead of process spawning is negligible for ~5–20 operations per module build session.

### 5. Full Feature Parity

If Lopen later needs GPG signing, sparse checkout, `git stash`, or any other feature, the CLI approach supports it immediately. LibGit2Sharp would require waiting for upstream support.

### 6. Simpler Debugging

When something goes wrong, developers can reproduce issues by running the exact same `git` command in their terminal. With LibGit2Sharp, debugging requires understanding the native interop layer.

### Recommended Implementation Pattern

```
IGitService (interface)
  └── GitCliService : IGitService (production — wraps System.Diagnostics.Process)
  └── FakeGitService : IGitService (tests — in-memory)
```

Define `IGitService` with the five operations needed:

```csharp
public interface IGitService
{
    Task<string> CommitAllAsync(string module, string taskName, CancellationToken ct = default);
    Task CreateBranchAsync(string branchName, CancellationToken ct = default);
    Task ResetToCommitAsync(string commitSha, CancellationToken ct = default);
    Task<DateTimeOffset?> GetLastCommitDateAsync(string filePath, CancellationToken ct = default);
    Task<string> GetDiffAsync(CancellationToken ct = default);
    Task<string> GetCommitDiffAsync(string commitSha, CancellationToken ct = default);
}
```

This keeps the Git interaction surface small, testable, and decoupled from the orchestrator logic.

---

## References

- [LibGit2Sharp NuGet](https://www.nuget.org/packages/LibGit2Sharp) — v0.31.0, December 2024
- [LibGit2Sharp GitHub](https://github.com/libgit2/libgit2sharp) — MIT license, active CI
- [LibGit2Sharp Wiki — Hitchhiker's Guide](https://github.com/libgit2/libgit2sharp/wiki/LibGit2Sharp-Hitchhiker%27s-Guide-to-Git)
- [LibGit2Sharp Wiki — git-commit](https://github.com/libgit2/libgit2sharp/wiki/git-commit)
- [LibGit2Sharp Wiki — git-branch](https://github.com/libgit2/libgit2sharp/wiki/git-branch)
- [LibGit2Sharp Wiki — git-diff](https://github.com/libgit2/libgit2sharp/wiki/git-diff)
- [LibGit2Sharp Wiki — git-reset](https://github.com/libgit2/libgit2sharp/wiki/git-reset)
- [System.Diagnostics.Process — .NET 8 docs](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process)
