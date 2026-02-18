using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Storage.Tests;

public sealed class AssessmentCacheTests
{
    private const string ProjectRoot = "/test/project";
    private readonly InMemoryFileSystem _fs = new();
    private readonly AssessmentCache _sut;

    public AssessmentCacheTests()
    {
        _fs.CreateDirectory(StoragePaths.GetAssessmentsCacheDirectory(ProjectRoot));
        _sut = new AssessmentCache(_fs, NullLogger<AssessmentCache>.Instance, ProjectRoot);
    }

    private void CreateSourceFile(string path, string content)
    {
        var dir = Path.GetDirectoryName(path)!;
        if (!_fs.DirectoryExists(dir))
            _fs.CreateDirectory(dir);
        _fs.WriteAllTextAsync(path, content).Wait();
    }

    private IReadOnlyDictionary<string, DateTime> CaptureTimestamps(params string[] paths)
    {
        var timestamps = new Dictionary<string, DateTime>();
        foreach (var path in paths)
            timestamps[path] = _fs.GetLastWriteTimeUtc(path);
        return timestamps;
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_NullFileSystem_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AssessmentCache(null!, NullLogger<AssessmentCache>.Instance, ProjectRoot));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AssessmentCache(new InMemoryFileSystem(), null!, ProjectRoot));
    }

    [Fact]
    public void Constructor_EmptyProjectRoot_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new AssessmentCache(new InMemoryFileSystem(), NullLogger<AssessmentCache>.Instance, ""));
    }

    // --- GetAsync ---

    [Fact]
    public async Task GetAsync_NotCached_ReturnsNull()
    {
        var result = await _sut.GetAsync("module:auth");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_EmptyScopeKey_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GetAsync(""));
    }

    // --- SetAsync + GetAsync round trip ---

    [Fact]
    public async Task SetAndGet_ReturnsCachedContent()
    {
        CreateSourceFile("/src/auth/login.cs", "code");
        var timestamps = CaptureTimestamps("/src/auth/login.cs");

        await _sut.SetAsync("auth:assessment", "assessment result", timestamps);
        var result = await _sut.GetAsync("auth:assessment");

        Assert.NotNull(result);
        Assert.Equal("assessment result", result.Content);
    }

    [Fact]
    public async Task SetAsync_EmptyScopeKey_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.SetAsync("", "content", new Dictionary<string, DateTime>()));
    }

    [Fact]
    public async Task SetAsync_NullContent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.SetAsync("scope", null!, new Dictionary<string, DateTime>()));
    }

    [Fact]
    public async Task SetAsync_NullTimestamps_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.SetAsync("scope", "content", null!));
    }

    // --- Invalidation on file change ---

    [Fact]
    public async Task GetAsync_FileInScopeModified_ReturnsNull()
    {
        CreateSourceFile("/src/auth/login.cs", "original");
        CreateSourceFile("/src/auth/register.cs", "original");
        var timestamps = CaptureTimestamps("/src/auth/login.cs", "/src/auth/register.cs");

        await _sut.SetAsync("auth:assessment", "cached", timestamps);

        // Modify one file
        await Task.Delay(10);
        CreateSourceFile("/src/auth/login.cs", "modified");

        var result = await _sut.GetAsync("auth:assessment");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_NoFilesChanged_ReturnsEntry()
    {
        CreateSourceFile("/src/auth/login.cs", "code");
        var timestamps = CaptureTimestamps("/src/auth/login.cs");

        await _sut.SetAsync("auth:assessment", "cached", timestamps);

        var result = await _sut.GetAsync("auth:assessment");
        Assert.NotNull(result);
        Assert.Equal("cached", result.Content);
    }

    // --- InvalidateAsync ---

    [Fact]
    public async Task InvalidateAsync_RemovesEntry()
    {
        CreateSourceFile("/src/auth/login.cs", "code");
        var timestamps = CaptureTimestamps("/src/auth/login.cs");

        await _sut.SetAsync("auth:assessment", "cached", timestamps);
        await _sut.InvalidateAsync("auth:assessment");

        Assert.Null(await _sut.GetAsync("auth:assessment"));
    }

    [Fact]
    public async Task InvalidateAsync_EmptyScopeKey_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.InvalidateAsync(""));
    }

    // --- ClearAsync ---

    [Fact]
    public async Task ClearAsync_RemovesAllEntries()
    {
        CreateSourceFile("/src/a.cs", "a");
        CreateSourceFile("/src/b.cs", "b");

        await _sut.SetAsync("scope-a", "a", CaptureTimestamps("/src/a.cs"));
        await _sut.SetAsync("scope-b", "b", CaptureTimestamps("/src/b.cs"));

        await _sut.ClearAsync();

        Assert.Null(await _sut.GetAsync("scope-a"));
        Assert.Null(await _sut.GetAsync("scope-b"));
    }

    // --- Disk persistence ---

    [Fact]
    public async Task GetAsync_ReadsFromDisk()
    {
        CreateSourceFile("/src/auth/login.cs", "code");
        var timestamps = CaptureTimestamps("/src/auth/login.cs");

        await _sut.SetAsync("auth:assessment", "persisted", timestamps);

        var freshCache = new AssessmentCache(_fs, NullLogger<AssessmentCache>.Instance, ProjectRoot);
        var result = await freshCache.GetAsync("auth:assessment");

        Assert.NotNull(result);
        Assert.Equal("persisted", result.Content);
    }

    // --- Corrupted entry ---

    [Fact]
    public async Task GetAsync_CorruptedEntry_ReturnsNull()
    {
        CreateSourceFile("/src/auth/login.cs", "code");
        var timestamps = CaptureTimestamps("/src/auth/login.cs");

        await _sut.SetAsync("auth:assessment", "valid", timestamps);

        // Corrupt the cache file
        var cacheDir = StoragePaths.GetAssessmentsCacheDirectory(ProjectRoot);
        var files = _fs.GetFiles(cacheDir, "*.json").ToList();
        Assert.NotEmpty(files);
        await _fs.WriteAllTextAsync(files[0], "corrupted{{{");

        var freshCache = new AssessmentCache(_fs, NullLogger<AssessmentCache>.Instance, ProjectRoot);
        var result = await freshCache.GetAsync("auth:assessment");

        Assert.Null(result); // Silently invalidated
    }

    // --- Metadata ---

    [Fact]
    public async Task SetAsync_RecordsCachedTimestamp()
    {
        CreateSourceFile("/src/auth/login.cs", "code");
        var timestamps = CaptureTimestamps("/src/auth/login.cs");
        var before = DateTime.UtcNow;

        await _sut.SetAsync("auth:assessment", "cached", timestamps);

        var result = await _sut.GetAsync("auth:assessment");
        Assert.NotNull(result);
        Assert.True(result.CachedAtUtc >= before);
        Assert.Equal(timestamps, result.FileTimestamps);
    }

    // --- Multiple scopes ---

    [Fact]
    public async Task SetAndGet_MultipleScopesIndependent()
    {
        CreateSourceFile("/src/auth/login.cs", "auth");
        CreateSourceFile("/src/storage/session.cs", "storage");

        await _sut.SetAsync("auth:assessment", "auth result", CaptureTimestamps("/src/auth/login.cs"));
        await _sut.SetAsync("storage:assessment", "storage result", CaptureTimestamps("/src/storage/session.cs"));

        Assert.Equal("auth result", (await _sut.GetAsync("auth:assessment"))!.Content);
        Assert.Equal("storage result", (await _sut.GetAsync("storage:assessment"))!.Content);
    }

    // --- Empty scope (no files) ---

    [Fact]
    public async Task SetAndGet_EmptyFileTimestamps_AlwaysValid()
    {
        var emptyTimestamps = new Dictionary<string, DateTime>();

        await _sut.SetAsync("empty:scope", "result", emptyTimestamps);

        var result = await _sut.GetAsync("empty:scope");
        Assert.NotNull(result);
        Assert.Equal("result", result.Content);
    }

    // --- IOException during delete is logged ---

    [Fact]
    public async Task InvalidateAsync_DeleteThrowsIOException_LogsDebugMessage()
    {
        var inner = new InMemoryFileSystem();
        inner.CreateDirectory(StoragePaths.GetAssessmentsCacheDirectory(ProjectRoot));
        var throwingFs = new ThrowingDeleteFileSystem(inner);
        var logger = new TestLogger<AssessmentCache>();
        var cache = new AssessmentCache(throwingFs, logger, ProjectRoot);

        var dir = Path.GetDirectoryName("/src/auth/login.cs")!;
        if (!inner.DirectoryExists(dir))
            inner.CreateDirectory(dir);
        await inner.WriteAllTextAsync("/src/auth/login.cs", "code");

        var timestamps = new Dictionary<string, DateTime>
        {
            ["/src/auth/login.cs"] = inner.GetLastWriteTimeUtc("/src/auth/login.cs")
        };

        await cache.SetAsync("auth:assessment", "cached", timestamps);

        throwingFs.ThrowOnDelete = true;

        var exception = await Record.ExceptionAsync(() => cache.InvalidateAsync("auth:assessment"));
        Assert.Null(exception);

        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Debug &&
            e.Message.Contains("Best-effort cache cleanup failed"));
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }

    private sealed class ThrowingDeleteFileSystem(InMemoryFileSystem inner) : IFileSystem
    {
        public bool ThrowOnDelete { get; set; }

        public void CreateDirectory(string path) => inner.CreateDirectory(path);
        public bool FileExists(string path) => inner.FileExists(path);
        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) => inner.ReadAllTextAsync(path, cancellationToken);
        public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default) => inner.WriteAllTextAsync(path, content, cancellationToken);
        public IEnumerable<string> GetFiles(string path, string searchPattern = "*") => inner.GetFiles(path, searchPattern);
        public IEnumerable<string> GetDirectories(string path) => inner.GetDirectories(path);
        public void MoveFile(string sourcePath, string destinationPath) => inner.MoveFile(sourcePath, destinationPath);
        public void DeleteFile(string path)
        {
            if (ThrowOnDelete)
                throw new IOException("Simulated delete failure");
            inner.DeleteFile(path);
        }
        public void CreateSymlink(string linkPath, string targetPath) => inner.CreateSymlink(linkPath, targetPath);
        public string? GetSymlinkTarget(string linkPath) => inner.GetSymlinkTarget(linkPath);
        public void DeleteDirectory(string path, bool recursive = true) => inner.DeleteDirectory(path, recursive);
        public DateTime GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);
    }
}
