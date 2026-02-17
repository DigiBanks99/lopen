# Research: .NET Storage Implementation Patterns

> **Date:** 2026-02-15
> **Sources:** .NET 10.0 SDK documentation, [github.com/xoofx/markdig](https://github.com/xoofx/markdig), [learn.microsoft.com/dotnet/standard/serialization/system-text-json](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json)

---

## 1. File System Operations in .NET

Lopen's `.lopen/` directory requires reliable directory creation, JSON file I/O, symlinks, and crash-safe writes. .NET 10.0 provides all necessary primitives.

### Directory Creation

`Directory.CreateDirectory` is idempotent — it no-ops if the directory already exists. No need to guard with `Directory.Exists`.

```csharp
// Safe to call unconditionally; creates all intermediate directories
Directory.CreateDirectory(Path.Combine(projectRoot, ".lopen", "sessions"));
Directory.CreateDirectory(Path.Combine(projectRoot, ".lopen", "modules"));
Directory.CreateDirectory(Path.Combine(projectRoot, ".lopen", "cache", "sections"));
Directory.CreateDirectory(Path.Combine(projectRoot, ".lopen", "cache", "assessments"));
```

### JSON File Read/Write

Use async file I/O for non-blocking operations:

```csharp
// Write
await File.WriteAllTextAsync(path, jsonContent);

// Read
string json = await File.ReadAllTextAsync(path);
```

For large files, prefer stream-based serialization:

```csharp
await using var stream = File.Create(path);
await JsonSerializer.SerializeAsync(stream, state, AppJsonContext.Default.SessionState);
```

### Symlink Management (.NET 6+)

```csharp
// Create symlink: .lopen/sessions/latest -> {session-id}/
// Delete existing symlink first (CreateSymbolicLink does not overwrite)
var latestLink = Path.Combine(sessionsDir, "latest");
if (Path.Exists(latestLink))
    Directory.Delete(latestLink, recursive: false);

Directory.CreateSymbolicLink(latestLink, sessionDir);

// Resolve symlink target
var info = new DirectoryInfo(latestLink);
var target = info.ResolveLinkTarget(returnFinalTarget: true);
```

**Platform note:** Symlinks require no elevation on Linux. On Windows, they require Developer Mode or elevated privileges. For cross-platform safety, fall back to writing the session ID to a `latest.txt` file if symlink creation fails.

### Atomic Writes (Write-Then-Rename)

The critical pattern for crash-safe persistence:

```csharp
public static async Task WriteAtomicAsync(string targetPath, string content)
{
    // Temp file in same directory ensures same filesystem (required for atomic rename)
    var tempPath = targetPath + ".tmp";
    await File.WriteAllTextAsync(tempPath, content);
    File.Move(tempPath, targetPath, overwrite: true); // atomic on most filesystems
}
```

`File.Move` with `overwrite: true` (.NET 5+) performs an atomic rename on Linux (uses `rename(2)` syscall). On NTFS, it uses `MoveFileEx` with `MOVEFILE_REPLACE_EXISTING`, which is also atomic.

### Relevance to Lopen

Every state save (`state.json`, `metrics.json`) should use the atomic write pattern. This directly satisfies the spec's "Crash-Safe" design principle — if Lopen is killed mid-write, either the old file or the new file exists, never a partial write.

---

## 2. JSON Serialization

The spec requires compact JSON by default with on-demand prettification. System.Text.Json with source generators provides AOT-compatible, high-performance serialization.

### Source Generators for AOT Compatibility

```csharp
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(SessionState))]
[JsonSerializable(typeof(SessionMetrics))]
[JsonSerializable(typeof(ProjectConfig))]
[JsonSerializable(typeof(AssessmentCacheEntry))]
public partial class CompactJsonContext : JsonSerializerContext { }

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(SessionState))]
[JsonSerializable(typeof(SessionMetrics))]
public partial class PrettyJsonContext : JsonSerializerContext { }
```

### Usage Pattern

```csharp
// Compact (default for storage)
string json = JsonSerializer.Serialize(state, CompactJsonContext.Default.SessionState);

// Pretty (for `lopen session show --format json`)
string json = JsonSerializer.Serialize(state, PrettyJsonContext.Default.SessionState);

// Deserialization (always uses compact context — format doesn't matter for reading)
var state = JsonSerializer.Deserialize(json, CompactJsonContext.Default.SessionState);
```

### AOT Safety Rules

- **Always** pass a `JsonTypeInfo<T>` or `JsonSerializerContext` to `Serialize`/`Deserialize` — never use parameterless overloads (those use reflection).
- Register all types via `[JsonSerializable(typeof(...))]`, including generic instantiations like `List<TaskNode>`.
- Set `JsonSerializerIsReflectionEnabledByDefault` to `false` in `.csproj` for build-time errors on accidental reflection use.
- Avoid `object` or `dynamic` properties — they require reflection.

```xml
<PropertyGroup>
    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
</PropertyGroup>
```

### Relevance to Lopen

Two serializer contexts (compact/pretty) directly map to the spec's "Compact by Default, Inspectable on Demand" principle. The compact context is used for all `.lopen/` persistence; the pretty context is used only for CLI display commands like `lopen session show --format json`.

---

## 3. Session State Management

The spec defines a session lifecycle: Create → Auto-Save → Interrupt/Resume → Complete → Cleanup. Crash-safe persistence is the critical requirement.

### Write-Then-Rename Pattern

As described in Section 1, all state writes use atomic rename. The full pattern for session state:

```csharp
public async Task SaveSessionStateAsync(string sessionDir, SessionState state)
{
    var statePath = Path.Combine(sessionDir, "state.json");
    var json = JsonSerializer.Serialize(state, CompactJsonContext.Default.SessionState);
    await WriteAtomicAsync(statePath, json);
}
```

### File Locking (Cross-Process Safety)

If multiple Lopen instances could access the same `.lopen/` directory:

```csharp
await using var lockStream = new FileStream(
    Path.Combine(lopenDir, ".lock"),
    FileMode.OpenOrCreate,
    FileAccess.ReadWrite,
    FileShare.None // exclusive lock
);
// Perform state operations while holding the lock
```

**Recommendation:** For Lopen's use case (single CLI process per project), file locking adds complexity without benefit. The atomic write pattern alone is sufficient. Only add locking if multi-instance support becomes a requirement.

### Session ID Generation

Per the spec, IDs follow `{module}-YYYYMMDD-{counter}`:

```csharp
public string GenerateSessionId(string moduleName, string sessionsDir)
{
    var date = DateTime.UtcNow.ToString("yyyyMMdd");
    var prefix = $"{moduleName}-{date}-";

    // Find highest existing counter for this module+date
    int counter = 0;
    if (Directory.Exists(sessionsDir))
    {
        counter = Directory.GetDirectories(sessionsDir)
            .Select(Path.GetFileName)
            .Where(name => name!.StartsWith(prefix))
            .Select(name => int.TryParse(name![prefix.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    return $"{prefix}{counter + 1}";
}
```

### Resume Detection

```csharp
public string? GetLatestSessionPath(string sessionsDir)
{
    var latestLink = Path.Combine(sessionsDir, "latest");
    if (!Path.Exists(latestLink)) return null;

    // Resolve symlink (or read latest.txt fallback)
    var info = new DirectoryInfo(latestLink);
    var target = info.ResolveLinkTarget(returnFinalTarget: true);
    return target?.FullName ?? info.FullName;
}
```

### Relevance to Lopen

This maps directly to the spec's session lifecycle. Atomic writes ensure crash-safety. The symlink-based `latest` pointer enables fast resume detection without scanning all session directories.

---

## 4. Markdown Parsing

Lopen extracts sections from specification documents for context management. This requires parsing markdown headings and extracting content between them.

### Markdig Library

**Package:** `Markdig` by xoofx (latest stable: 0.45.0)
**Compatibility:** .NET Standard 2.0+, fully compatible with .NET 10.0
**Performance:** One of the fastest managed Markdown parsers; CommonMark compliant with 20+ extensions.

### Parsing and Section Extraction

```csharp
using Markdig;
using Markdig.Syntax;

public record MarkdownSection(string Header, int Level, int StartLine, int EndLine, string Content);

public List<MarkdownSection> ExtractSections(string markdown)
{
    var document = Markdown.Parse(markdown);
    var blocks = document.ToList();
    var lines = markdown.Split('\n');
    var sections = new List<MarkdownSection>();

    for (int i = 0; i < blocks.Count; i++)
    {
        if (blocks[i] is not HeadingBlock heading) continue;

        var headerText = heading.Inline?.FirstChild?.ToString() ?? "";
        int startLine = heading.Line;

        // Find the end: next heading at same or higher level, or end of document
        int endLine = lines.Length - 1;
        for (int j = i + 1; j < blocks.Count; j++)
        {
            if (blocks[j] is HeadingBlock next && next.Level <= heading.Level)
            {
                endLine = next.Line - 1;
                break;
            }
        }

        var content = string.Join('\n', lines[startLine..(endLine + 1)]);
        sections.Add(new MarkdownSection(headerText, heading.Level, startLine, endLine, content));
    }

    return sections;
}
```

### Key AST Node Types

| Node Type            | Description                             |
| -------------------- | --------------------------------------- |
| `MarkdownDocument`   | Root node (extends `ContainerBlock`)    |
| `HeadingBlock`       | Headings (`.Level` = 1–6)              |
| `ParagraphBlock`     | Paragraphs                              |
| `ListBlock`          | Lists (ordered/unordered)               |
| `ListItemBlock`      | Individual list items                   |
| `FencedCodeBlock`    | Fenced code blocks                      |
| `ThematicBreakBlock` | Horizontal rules (`---`)                |

### YAML Frontmatter

Markdig supports YAML frontmatter via the `UseYamlFrontMatter()` extension:

```csharp
var pipeline = new MarkdownPipelineBuilder()
    .UseYamlFrontMatter()
    .Build();

var document = Markdown.Parse(markdown, pipeline);
var frontmatter = document.Descendants<Markdig.Extensions.Yaml.YamlFrontMatterBlock>().FirstOrDefault();
```

### Relevance to Lopen

Section extraction is core to the spec's document management: Lopen reads specifications from `docs/requirements/` and extracts specific sections to include in LLM context. Markdig's AST-based parsing is more reliable than regex-based approaches and handles edge cases (code blocks containing `#`, nested headings, etc.). The YAML frontmatter extension parses the `name`/`description` metadata in specification files.

---

## 5. Caching Strategy

The spec defines two cache types: section cache (keyed by file path + header + modification time) and assessment cache (short-lived, invalidated on file changes).

### In-Memory Cache with File Modification Tracking

```csharp
public record SectionCacheKey(string FilePath, string SectionHeader);

public record CachedSection(string Content, DateTime FileModifiedUtc);

public class SectionCache
{
    private readonly ConcurrentDictionary<SectionCacheKey, CachedSection> _cache = new();

    public string? Get(string filePath, string sectionHeader)
    {
        var key = new SectionCacheKey(filePath, sectionHeader);
        if (!_cache.TryGetValue(key, out var cached)) return null;

        // Invalidate if file has changed
        var currentModified = File.GetLastWriteTimeUtc(filePath);
        if (currentModified != cached.FileModifiedUtc)
        {
            _cache.TryRemove(key, out _);
            return null;
        }

        return cached.Content;
    }

    public void Set(string filePath, string sectionHeader, string content)
    {
        var key = new SectionCacheKey(filePath, sectionHeader);
        var modified = File.GetLastWriteTimeUtc(filePath);
        _cache[key] = new CachedSection(content, modified);
    }
}
```

### Disk-Backed Cache (for `.lopen/cache/sections/`)

The spec stores cached sections on disk. A hash-based key maps to cache files:

```csharp
public string GetCachePath(string cacheDir, string filePath, string sectionHeader)
{
    // Deterministic filename from the cache key
    var keyBytes = Encoding.UTF8.GetBytes($"{filePath}::{sectionHeader}");
    var hash = Convert.ToHexString(SHA256.HashData(keyBytes))[..16];
    return Path.Combine(cacheDir, "sections", $"{hash}.json");
}
```

### File Modification Timestamp Notes

`File.GetLastWriteTimeUtc` returns `1601-01-01 00:00:00 UTC` if the file does not exist (no exception thrown). Filesystem timestamp resolution varies:

| Filesystem | Resolution |
| ---------- | ---------- |
| NTFS       | 100 ns     |
| ext4       | 1 ns       |
| FAT32      | 2 s        |

For reliability, compare timestamps with a small tolerance or also include file size.

### Relevance to Lopen

The dual-layer cache (in-memory + disk) optimizes repeated section reads within a session (memory) and across sessions (disk). The modification-timestamp invalidation ensures cache correctness without expensive content hashing. This directly satisfies the spec's section cache requirements.

---

## 6. Error Handling

The spec defines three error categories: corrupted state, disk full/write failure, and cache corruption. Each has distinct handling.

### Corrupted State Detection

```csharp
public SessionState? TryLoadSessionState(string statePath)
{
    try
    {
        var json = File.ReadAllText(statePath);
        return JsonSerializer.Deserialize(json, CompactJsonContext.Default.SessionState);
    }
    catch (JsonException ex)
    {
        // Malformed JSON
        Log.Warning("Corrupted session state at {Path}: {Error}", statePath, ex.Message);
        MoveToCorrupted(statePath);
        return null;
    }
    catch (IOException ex)
    {
        Log.Warning("Unreadable session state at {Path}: {Error}", statePath, ex.Message);
        return null;
    }
}

private void MoveToCorrupted(string filePath)
{
    var corruptedDir = Path.Combine(_lopenDir, "corrupted");
    Directory.CreateDirectory(corruptedDir);
    var dest = Path.Combine(corruptedDir, $"{Path.GetFileName(filePath)}.{DateTime.UtcNow:yyyyMMddHHmmss}");
    File.Move(filePath, dest);
}
```

### Disk Full / Permission Errors

```csharp
public async Task SafeWriteAsync(string path, string content)
{
    try
    {
        await WriteAtomicAsync(path, content);
    }
    catch (IOException ex) when (IsDiskFull(ex))
    {
        throw new CriticalStorageException($"Disk full — cannot write to {path}", ex);
    }
    catch (UnauthorizedAccessException ex)
    {
        throw new CriticalStorageException($"Permission denied — cannot write to {path}", ex);
    }
}

private static bool IsDiskFull(IOException ex)
{
    // ERROR_DISK_FULL (0x70) or ERROR_HANDLE_DISK_FULL (0x27)
    const int ERROR_DISK_FULL = unchecked((int)0x80070070);
    const int ERROR_HANDLE_DISK_FULL = unchecked((int)0x80070027);
    // Linux ENOSPC = 28
    const int ENOSPC = 28;

    return ex.HResult == ERROR_DISK_FULL
        || ex.HResult == ERROR_HANDLE_DISK_FULL
        || ex.HResult == ENOSPC;
}
```

### Cache Corruption (Silent Recovery)

```csharp
public string? TryGetCachedSection(string cachePath)
{
    try
    {
        var json = File.ReadAllText(cachePath);
        var entry = JsonSerializer.Deserialize(json, CompactJsonContext.Default.CacheEntry);
        return entry?.Content;
    }
    catch
    {
        // Silently invalidate — cache is always re-derivable
        try { File.Delete(cachePath); } catch { /* best-effort cleanup */ }
        return null;
    }
}
```

### Relevance to Lopen

The three-tier error strategy matches the spec exactly: corrupted state → warn + move to `.lopen/corrupted/`; disk full → critical error + pause workflow; cache corruption → silent regeneration. The `CriticalStorageException` type should integrate with Core's failure handling.

---

## 7. Recommended NuGet Packages

| Package              | Purpose                             | Notes                                       |
| -------------------- | ----------------------------------- | ------------------------------------------- |
| `System.Text.Json`   | JSON serialization/deserialization  | Built into .NET 10.0 SDK; no extra package  |
| `Markdig`            | Markdown parsing and AST extraction | Latest stable: 0.45.0. CommonMark compliant  |

### Packages NOT Needed

| Package                            | Why Not                                                                                      |
| ---------------------------------- | -------------------------------------------------------------------------------------------- |
| `Newtonsoft.Json`                  | System.Text.Json is built-in, AOT-compatible, and faster                                     |
| `Microsoft.Extensions.Caching.*`  | Overkill for file-based caching; a simple `ConcurrentDictionary` suffices                    |
| `YamlDotNet`                       | Markdig's YAML frontmatter extension handles the limited YAML parsing needed                 |
| `Polly`                            | Retry logic for file I/O is simple enough to implement inline                                |

### Relevance to Lopen

Minimal dependencies keep the CLI lightweight and AOT-friendly. System.Text.Json is zero-cost (built-in). Markdig is the only external dependency needed for storage.

---

## 8. Plan Management (Markdown Checkboxes)

The spec requires plans stored at `.lopen/modules/{module}/plan.md` with checkbox task hierarchies, updated programmatically by Lopen (not by the LLM).

### Writing Plans

Plans use standard GitHub-flavored markdown checkboxes:

```markdown
## Components

- [ ] AuthController
  - [ ] Implement login endpoint
  - [x] Add JWT validation
  - [ ] Write integration tests
- [ ] UserService
  - [ ] Create user repository
```

### Programmatic Checkbox Updates

Markdig can parse task lists via the `UseTaskLists()` extension, but for **updating** checkboxes the most reliable approach is line-based text manipulation — Markdig's AST is primarily designed for reading, not round-trip editing:

```csharp
public string UpdateTaskStatus(string planContent, string taskText, bool completed)
{
    var lines = planContent.Split('\n');
    for (int i = 0; i < lines.Length; i++)
    {
        var trimmed = lines[i].TrimStart();
        if (trimmed.StartsWith("- [ ] ") || trimmed.StartsWith("- [x] "))
        {
            var text = trimmed[6..].Trim();
            if (text.Equals(taskText, StringComparison.OrdinalIgnoreCase))
            {
                var prefix = lines[i][..lines[i].IndexOf("- [")];
                lines[i] = $"{prefix}- [{(completed ? 'x' : ' ')}] {text}";
                break;
            }
        }
    }
    return string.Join('\n', lines);
}
```

### Reading Task Status

For **reading** task status, Markdig's `TaskList` extension provides structured access:

```csharp
var pipeline = new MarkdownPipelineBuilder()
    .UseTaskLists()
    .Build();

var document = Markdown.Parse(planContent, pipeline);
foreach (var listItem in document.Descendants<ListItemBlock>())
{
    if (listItem.Count > 0 && listItem[0] is ParagraphBlock para)
    {
        var inline = para.Inline?.FirstChild;
        if (inline is Markdig.Extensions.TaskLists.TaskList taskList)
        {
            bool isChecked = taskList.Checked;
            var taskText = inline.NextSibling?.ToString()?.Trim();
            // Process task status...
        }
    }
}
```

### Relevance to Lopen

Plan files are the human-inspectable view of task progress. Using line-based editing for updates avoids AST round-trip issues, while Markdig's TaskList extension provides structured reading. Plans are written atomically (same write-then-rename pattern as state files).

---

## 9. Assessment Cache

The spec defines assessment cache as short-lived, invalidated on any file change in the assessed scope. This differs from the section cache (which tracks individual file timestamps).

### Scope-Based Invalidation

Assessment cache entries track a set of files (the assessed scope) and a snapshot of their modification times:

```csharp
public record AssessmentCacheEntry(
    string AssessmentResult,
    DateTime CachedAtUtc,
    Dictionary<string, DateTime> FileTimestamps // filePath -> lastWriteTimeUtc
);

public class AssessmentCache
{
    private readonly string _cacheDir;

    public string? Get(string scopeKey, IEnumerable<string> filePaths)
    {
        var cachePath = GetCachePath(scopeKey);
        if (!File.Exists(cachePath)) return null;

        try
        {
            var json = File.ReadAllText(cachePath);
            var entry = JsonSerializer.Deserialize(json, CompactJsonContext.Default.AssessmentCacheEntry);
            if (entry is null) return null;

            // Invalidate if ANY file in scope has changed
            foreach (var (path, cachedTime) in entry.FileTimestamps)
            {
                if (!File.Exists(path)) return null;
                if (File.GetLastWriteTimeUtc(path) != cachedTime) return null;
            }

            return entry.AssessmentResult;
        }
        catch
        {
            try { File.Delete(cachePath); } catch { }
            return null;
        }
    }

    public void Set(string scopeKey, string result, IEnumerable<string> filePaths)
    {
        var timestamps = filePaths.ToDictionary(
            f => f,
            f => File.GetLastWriteTimeUtc(f)
        );
        var entry = new AssessmentCacheEntry(result, DateTime.UtcNow, timestamps);
        var json = JsonSerializer.Serialize(entry, CompactJsonContext.Default.AssessmentCacheEntry);
        WriteAtomicAsync(GetCachePath(scopeKey), json).GetAwaiter().GetResult();
    }

    private string GetCachePath(string scopeKey)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scopeKey)))[..16];
        return Path.Combine(_cacheDir, "assessments", $"{hash}.json");
    }
}
```

### Relevance to Lopen

The scope-based invalidation ensures correctness: if any file in the assessed directory changes, the assessment is re-run. This is more aggressive than section cache invalidation (which is per-file) because assessment results depend on the relationship between files, not individual files.

---

## 10. Session Pruning

The spec requires completed sessions to be retained up to a configurable `session_retention` limit, with oldest pruned first.

### Implementation Pattern

```csharp
public void PruneCompletedSessions(string sessionsDir, int retentionLimit)
{
    if (retentionLimit <= 0) return; // 0 = unlimited retention

    var sessionDirs = Directory.GetDirectories(sessionsDir)
        .Where(d => !Path.GetFileName(d).Equals("latest", StringComparison.OrdinalIgnoreCase))
        .Select(d => new { Path = d, State = TryLoadSessionState(Path.Combine(d, "state.json")) })
        .Where(s => s.State?.Status == "complete")
        .OrderBy(s => s.State!.CompletedAtUtc)
        .ToList();

    // Keep the most recent `retentionLimit` completed sessions
    var toRemove = sessionDirs.Count - retentionLimit;
    if (toRemove <= 0) return;

    foreach (var session in sessionDirs.Take(toRemove))
    {
        try
        {
            Directory.Delete(session.Path, recursive: true);
        }
        catch (IOException ex)
        {
            Log.Warning("Failed to prune session {Path}: {Error}", session.Path, ex.Message);
        }
    }
}
```

### When to Prune

Pruning runs on workflow startup (after resume detection), not during active workflows. This keeps the hot path simple and avoids deleting sessions that might be referenced.

### Relevance to Lopen

This directly satisfies the spec's acceptance criterion: "Completed sessions are retained up to the configured `session_retention` limit, then pruned." The oldest-first ordering ensures the most recent sessions are always available for reference.

---

## 11. Implementation Approach

### Directory Structure Creation

On first workflow run, create the full `.lopen/` tree:

```csharp
public class StorageInitializer
{
    public void EnsureDirectoryStructure(string projectRoot)
    {
        var lopenDir = Path.Combine(projectRoot, ".lopen");
        var dirs = new[]
        {
            Path.Combine(lopenDir, "sessions"),
            Path.Combine(lopenDir, "modules"),
            Path.Combine(lopenDir, "cache", "sections"),
            Path.Combine(lopenDir, "cache", "assessments"),
            Path.Combine(lopenDir, "corrupted"),
        };

        // Note: Per-session history/ directories are created when a session starts
        // (if save_iteration_history is enabled), not during global init.

        foreach (var dir in dirs)
            Directory.CreateDirectory(dir);

        // Ensure .lopen/ is in .gitignore
        EnsureGitignoreEntry(projectRoot, ".lopen/");
    }

    private void EnsureGitignoreEntry(string projectRoot, string entry)
    {
        var gitignorePath = Path.Combine(projectRoot, ".gitignore");
        if (File.Exists(gitignorePath))
        {
            var content = File.ReadAllText(gitignorePath);
            if (content.Contains(entry)) return;
            File.AppendAllText(gitignorePath, $"\n{entry}\n");
        }
    }
}
```

### Session Lifecycle

```
┌──────────┐     ┌───────────┐     ┌───────────┐     ┌──────────┐
│  Create   │────►│ Auto-Save │────►│ Complete  │────►│ Cleanup  │
│ session   │     │ on events │     │ workflow  │     │ prune    │
└──────────┘     └─────┬─────┘     └───────────┘     └──────────┘
                       │
                  ┌────▼────┐
                  │Interrupt│
                  │ (crash) │
                  └────┬────┘
                       │
                  ┌────▼────┐
                  │ Resume  │
                  │on start │
                  └─────────┘
```

1. **Create**: Generate session ID → create directory → write initial `state.json` + `metrics.json` → update `latest` symlink
2. **Auto-Save**: After each save trigger (step/task/phase completion), atomic-write `state.json`
3. **Resume**: Check `latest` symlink → load `state.json` → validate integrity → offer resume/fresh
4. **Complete**: Mark session state as complete → retain for reference
5. **Cleanup**: On startup, prune completed sessions beyond retention limit (oldest first)

### Section Cache Implementation

```csharp
public class SectionCacheService
{
    private readonly ConcurrentDictionary<SectionCacheKey, CachedSection> _memory = new();
    private readonly string _diskCacheDir;

    public string? GetSection(string filePath, string sectionHeader)
    {
        // 1. Check in-memory cache
        var memResult = TryGetFromMemory(filePath, sectionHeader);
        if (memResult is not null) return memResult;

        // 2. Check disk cache
        var diskResult = TryGetFromDisk(filePath, sectionHeader);
        if (diskResult is not null)
        {
            // Promote to memory cache
            SetInMemory(filePath, sectionHeader, diskResult);
            return diskResult;
        }

        return null; // Cache miss — caller must extract from source
    }

    public void Set(string filePath, string sectionHeader, string content)
    {
        SetInMemory(filePath, sectionHeader, content);
        WriteToDisk(filePath, sectionHeader, content);
    }
}
```

### Key Design Decisions

| Decision                        | Choice                  | Rationale                                                 |
| ------------------------------- | ----------------------- | --------------------------------------------------------- |
| Serialization library           | System.Text.Json        | Built-in, AOT-compatible, high performance                |
| Serialization approach          | Source generators        | Required for AOT; two contexts for compact/pretty         |
| Atomic write strategy           | Write-temp + rename     | Crash-safe, well-tested pattern, works on Linux and Windows |
| Symlink fallback                | `latest.txt` file       | Windows without Developer Mode cannot create symlinks     |
| Markdown parser                 | Markdig                 | Fast, CommonMark-compliant, good AST API                  |
| In-memory cache structure       | `ConcurrentDictionary`  | Thread-safe, simple, no external dependency               |
| Cache invalidation              | File modification time  | Low overhead, no content hashing needed                   |
| Assessment cache invalidation   | Scope-based timestamps  | Any change in assessed scope invalidates the cache        |
| Plan checkbox updates           | Line-based text editing | More reliable than AST round-trip for targeted edits      |
| Session pruning                 | Startup-time, oldest-first | Avoids interference with active workflows              |
| Error handling for corruption   | Move to `corrupted/`    | Preserves evidence for debugging without blocking startup |

### Relevance to Lopen

This approach satisfies all acceptance criteria in the spec: atomic writes for crash-safety, symlink-based resume detection, dual-layer caching (section + assessment), three-tier error handling, plan checkbox management, session pruning, and minimal dependencies. The implementation is AOT-compatible throughout, keeping the CLI startup fast.

---

## 12. Corruption Detection and Quarantine Testing (STOR-14)

STOR-14 requires that corrupted session state is detected, warned, and excluded from resume. STOR-15 requires corrupted files are moved to `.lopen/corrupted/`. This section covers how to create corrupt JSON for testing and verify the detection/quarantine flow.

### Creating Corrupt JSON for Tests

Tests need multiple corruption categories:

```csharp
// 1. Truncated JSON (simulates crash mid-write)
var truncated = """{"session_id":"test-20260215-1","phase":"req""";

// 2. Completely invalid content (binary garbage or empty)
var garbage = "NOT-JSON-\x00\x01\x02";
var empty = "";

// 3. Valid JSON but wrong schema (missing required fields)
var wrongSchema = """{"unexpected_field": true}""";

// 4. Valid JSON but wrong types (e.g., number where string expected)
var wrongTypes = """{"session_id": 12345, "phase": null}""";
```

### Testing the Detection Flow with InMemoryFileSystem

The existing `InMemoryFileSystem` supports seeding corrupt content directly:

```csharp
[Fact]
public async Task LoadSession_CorruptedJson_ReturnsNullAndQuarantines()
{
    var fs = new InMemoryFileSystem();
    var projectRoot = "/project";
    var sessionId = SessionId.Parse("test-20260215-1");

    // Seed a corrupt state.json
    var statePath = StoragePaths.GetSessionStatePath(projectRoot, sessionId);
    fs.CreateDirectory(Path.GetDirectoryName(statePath)!);
    await fs.WriteAllTextAsync(statePath, """{"session_id":"test""");

    var mgr = new SessionManager(fs, NullLogger<SessionManager>.Instance, projectRoot);

    // Act: attempt to load
    var result = await mgr.GetLatestSessionAsync(CancellationToken.None);

    // Assert: returns null (session excluded from resume)
    Assert.Null(result);
}
```

### Verifying the Quarantine Flow

The quarantine path uses `StoragePaths.GetCorruptedDirectory()`, which resolves to `.lopen/corrupted/`. The file is moved with a timestamp suffix to avoid collisions:

```csharp
// Expected quarantine behavior:
// 1. JsonException is caught during deserialization
// 2. Logger.LogWarning is called with the file path and error
// 3. File is moved: .lopen/sessions/{id}/state.json → .lopen/corrupted/state.json.20260215120000
// 4. The session is excluded from GetLatestSessionAsync results
```

### Key IOException vs JsonException Distinction

| Exception Type | Meaning | Action |
|---|---|---|
| `JsonException` | Content is corrupt/malformed | Quarantine to `.lopen/corrupted/`, warn, exclude from resume |
| `FileNotFoundException` | File missing (not corruption) | Return null, no quarantine needed |
| `IOException` | Disk/permission error | Throw `StorageException`, do not quarantine |

### Relevance to Lopen

The `InMemoryFileSystem` abstraction makes corruption testing straightforward — seed bad content via `WriteAllTextAsync` and verify the detection path. No actual filesystem corruption or temp files needed. Tests should cover all four corruption categories (truncated, garbage, wrong schema, wrong types) to ensure `JsonSerializer.Deserialize` failures are caught and quarantined correctly.

---

## 13. Disk-Full Detection and Testing (STOR-16)

STOR-16 requires that disk full / write failure is treated as a critical system error and pauses the workflow. This section covers how .NET exposes disk-full errors, the specific `IOException` types, and how to test with mock filesystems.

### How .NET Exposes Disk-Full Errors

Disk-full conditions surface as `IOException` with platform-specific `HResult` values:

```csharp
private static bool IsDiskFull(IOException ex)
{
    // Windows: ERROR_DISK_FULL (0x70) and ERROR_HANDLE_DISK_FULL (0x27)
    const int ERROR_DISK_FULL = unchecked((int)0x80070070);
    const int ERROR_HANDLE_DISK_FULL = unchecked((int)0x80070027);

    // Linux/macOS: ENOSPC (28) — mapped by .NET as 0x80131620 + native errno
    // .NET on Linux wraps this as IOException with HResult = 0x80070000 | 28
    const int LINUX_ENOSPC = unchecked((int)0x8007001C); // 0x1C = 28

    return ex.HResult == ERROR_DISK_FULL
        || ex.HResult == ERROR_HANDLE_DISK_FULL
        || ex.HResult == LINUX_ENOSPC;
}
```

### IOException Type Hierarchy for Storage Errors

| Error Condition | Exception Type | HResult (Windows) | HResult (Linux) |
|---|---|---|---|
| Disk full | `IOException` | `0x80070070` or `0x80070027` | `0x8007001C` |
| Permission denied | `UnauthorizedAccessException` | `0x80070005` | `0x80131602` |
| Path not found | `DirectoryNotFoundException` | `0x80070003` | `0x80131523` |
| File in use | `IOException` | `0x80070020` | N/A (advisory locks) |
| Read-only filesystem | `IOException` | `0x80070013` | `0x8007001E` |

### Testing with FailingWriteFileSystem

The existing `FailingWriteFileSystem` in `SessionManagerTests.cs` already demonstrates the pattern — it throws `IOException("Disk full")` from `WriteAllTextAsync` and `MoveFile`:

```csharp
private sealed class FailingWriteFileSystem : IFileSystem
{
    public Task WriteAllTextAsync(string path, string content, CancellationToken ct)
        => throw new IOException("Disk full");
    public void MoveFile(string src, string dst)
        => throw new IOException("Disk full");
    // ... other members return defaults
}
```

For more precise testing, the mock can throw with a specific HResult:

```csharp
private sealed class DiskFullFileSystem : IFileSystem
{
    public Task WriteAllTextAsync(string path, string content, CancellationToken ct)
    {
        var ex = new IOException("There is not enough space on the disk.");
        // Set HResult via reflection or use the constructor:
        // IOException marshals HResult from the OS; for tests, set directly
        typeof(Exception).GetProperty("HResult")!.SetValue(ex, unchecked((int)0x80070070));
        throw ex;
    }
    // ...
}
```

However, since `SessionManager` already wraps all write `IOException`s into `StorageException`, the simpler `FailingWriteFileSystem` approach is sufficient for unit tests — the HResult-based detection is an implementation detail of the production `SafeWriteAsync` helper.

### Recommended Test Cases

```csharp
[Fact]
public async Task CreateSession_DiskFull_ThrowsStorageException()
{
    var fs = new FailingWriteFileSystem();
    var mgr = new SessionManager(fs, NullLogger<SessionManager>.Instance, "/project");

    var ex = await Assert.ThrowsAsync<StorageException>(
        () => mgr.CreateSessionAsync("test"));
    Assert.Contains("Disk full", ex.InnerException?.Message ?? ex.Message);
}

[Fact]
public async Task SaveState_DiskFull_ThrowsStorageException()
{
    // Similar pattern — verify StorageException wraps the IOException
}
```

### Relevance to Lopen

The `IFileSystem` abstraction makes disk-full testing clean — no need for actual disk exhaustion or OS-level mocking. The `FailingWriteFileSystem` pattern (already in the test suite) proves that `SessionManager` correctly converts `IOException` into `StorageException`. The caller (workflow orchestrator) catches `StorageException` and pauses the workflow, satisfying STOR-16.

---

## 14. Research Document Path Management (STOR-20)

STOR-20 requires that research documents are stored at `docs/requirements/{module}/RESEARCH-{topic}.md` in the source tree (not `.lopen/`). This section covers adding path constants to `StoragePaths` for research document paths.

### Current StoragePaths Structure

`StoragePaths` currently provides paths for `.lopen/` internal storage:

- `GetRoot` → `.lopen/`
- `GetSessionsDirectory` → `.lopen/sessions/`
- `GetModulesDirectory` → `.lopen/modules/`
- `GetCacheDirectory` → `.lopen/cache/`
- `GetCorruptedDirectory` → `.lopen/corrupted/`

Research documents live outside `.lopen/` — they are source-controlled documentation, not runtime state.

### Proposed Additions to StoragePaths

```csharp
/// <summary>Name of the requirements documentation directory.</summary>
public const string RequirementsDirectoryName = "docs/requirements";

/// <summary>Returns the docs/requirements/ directory path.</summary>
public static string GetRequirementsDirectory(string projectRoot) =>
    Path.Combine(projectRoot, "docs", "requirements");

/// <summary>Returns the docs/requirements/{module}/ directory path.</summary>
public static string GetModuleRequirementsDirectory(string projectRoot, string moduleName) =>
    Path.Combine(GetRequirementsDirectory(projectRoot), moduleName);

/// <summary>Returns the path to RESEARCH.md for a module.</summary>
public static string GetModuleResearchPath(string projectRoot, string moduleName) =>
    Path.Combine(GetModuleRequirementsDirectory(projectRoot, moduleName), "RESEARCH.md");

/// <summary>Returns the path to a topic-specific research document.</summary>
public static string GetModuleTopicResearchPath(string projectRoot, string moduleName, string topic) =>
    Path.Combine(GetModuleRequirementsDirectory(projectRoot, moduleName), $"RESEARCH-{topic}.md");

/// <summary>Returns the path to SPECIFICATION.md for a module.</summary>
public static string GetModuleSpecificationPath(string projectRoot, string moduleName) =>
    Path.Combine(GetModuleRequirementsDirectory(projectRoot, moduleName), "SPECIFICATION.md");
```

### Path Convention

The naming convention `RESEARCH-{topic}.md` allows multiple research documents per module:

```
docs/requirements/storage/
├── RESEARCH.md              ← main research (GetModuleResearchPath)
├── RESEARCH-caching.md      ← topic-specific (GetModuleTopicResearchPath)
├── RESEARCH-aspire.md       ← topic-specific (GetModuleTopicResearchPath)
└── SPECIFICATION.md         ← specification (GetModuleSpecificationPath)
```

### Why StoragePaths and Not a Separate Class

1. **Single source of truth** — all Lopen path resolution in one place
2. **Consistency** — same `projectRoot`-relative pattern as `.lopen/` paths
3. **Discoverability** — developers find all path helpers via `StoragePaths.Get*`
4. **No breaking changes** — additive methods only, existing API unchanged

### Test Patterns

```csharp
[Fact]
public void GetModuleResearchPath_ReturnsExpectedPath()
{
    var result = StoragePaths.GetModuleResearchPath("/project", "storage");
    Assert.Equal(Path.Combine("/project", "docs", "requirements", "storage", "RESEARCH.md"), result);
}

[Fact]
public void GetModuleTopicResearchPath_ReturnsExpectedPath()
{
    var result = StoragePaths.GetModuleTopicResearchPath("/project", "otel", "aspire");
    Assert.Equal(Path.Combine("/project", "docs", "requirements", "otel", "RESEARCH-aspire.md"), result);
}
```

### Relevance to Lopen

Adding research document paths to `StoragePaths` creates a consistent API for the workflow orchestrator and document management code to resolve spec/research file locations. This is essential for features like specification drift detection (`SpecificationDriftService`), module scanning (`ModuleScanner`), and context extraction — all of which need to locate `docs/requirements/{module}/` files. The paths are project-root-relative, matching the existing convention.
