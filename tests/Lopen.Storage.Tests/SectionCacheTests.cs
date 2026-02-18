using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Storage.Tests;

public sealed class SectionCacheTests
{
    private const string ProjectRoot = "/test/project";
    private readonly InMemoryFileSystem _fs = new();
    private readonly SectionCache _sut;

    public SectionCacheTests()
    {
        _fs.CreateDirectory(StoragePaths.GetSectionsCacheDirectory(ProjectRoot));
        _sut = new SectionCache(_fs, NullLogger<SectionCache>.Instance, ProjectRoot);
    }

    private void CreateSourceFile(string path, string content)
    {
        var dir = Path.GetDirectoryName(path)!;
        if (!_fs.DirectoryExists(dir))
            _fs.CreateDirectory(dir);
        _fs.WriteAllTextAsync(path, content).Wait();
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_NullFileSystem_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SectionCache(null!, NullLogger<SectionCache>.Instance, ProjectRoot));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SectionCache(new InMemoryFileSystem(), null!, ProjectRoot));
    }

    [Fact]
    public void Constructor_EmptyProjectRoot_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SectionCache(new InMemoryFileSystem(), NullLogger<SectionCache>.Instance, ""));
    }

    // --- GetAsync ---

    [Fact]
    public async Task GetAsync_NotCached_ReturnsNull()
    {
        var result = await _sut.GetAsync("/some/file.md", "Introduction");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_EmptyFilePath_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GetAsync("", "header"));
    }

    [Fact]
    public async Task GetAsync_EmptyHeader_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GetAsync("/file.md", ""));
    }

    // --- SetAsync + GetAsync round trip ---

    [Fact]
    public async Task SetAndGet_ReturnsCachedContent()
    {
        CreateSourceFile("/src/spec.md", "spec content");

        await _sut.SetAsync("/src/spec.md", "Overview", "Section content here");
        var result = await _sut.GetAsync("/src/spec.md", "Overview");

        Assert.NotNull(result);
        Assert.Equal("Section content here", result.Content);
    }

    [Fact]
    public async Task SetAsync_EmptyFilePath_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.SetAsync("", "header", "content"));
    }

    [Fact]
    public async Task SetAsync_EmptyHeader_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.SetAsync("/file.md", "", "content"));
    }

    [Fact]
    public async Task SetAsync_NullContent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.SetAsync("/file.md", "header", null!));
    }

    // --- Invalidation on file change ---

    [Fact]
    public async Task GetAsync_FileModified_ReturnsNull()
    {
        CreateSourceFile("/src/spec.md", "original");

        await _sut.SetAsync("/src/spec.md", "Overview", "cached content");

        // Simulate file modification by writing new content (changes mtime)
        await Task.Delay(10); // ensure different timestamp
        CreateSourceFile("/src/spec.md", "modified");

        var result = await _sut.GetAsync("/src/spec.md", "Overview");
        Assert.Null(result);
    }

    // --- InvalidateFileAsync ---

    [Fact]
    public async Task InvalidateFileAsync_RemovesCachedEntries()
    {
        CreateSourceFile("/src/spec.md", "content");

        await _sut.SetAsync("/src/spec.md", "Section1", "content1");
        await _sut.SetAsync("/src/spec.md", "Section2", "content2");

        await _sut.InvalidateFileAsync("/src/spec.md");

        Assert.Null(await _sut.GetAsync("/src/spec.md", "Section1"));
        Assert.Null(await _sut.GetAsync("/src/spec.md", "Section2"));
    }

    [Fact]
    public async Task InvalidateFileAsync_EmptyPath_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.InvalidateFileAsync(""));
    }

    // --- ClearAsync ---

    [Fact]
    public async Task ClearAsync_RemovesAllEntries()
    {
        CreateSourceFile("/src/a.md", "a");
        CreateSourceFile("/src/b.md", "b");

        await _sut.SetAsync("/src/a.md", "Header", "content a");
        await _sut.SetAsync("/src/b.md", "Header", "content b");

        await _sut.ClearAsync();

        Assert.Null(await _sut.GetAsync("/src/a.md", "Header"));
        Assert.Null(await _sut.GetAsync("/src/b.md", "Header"));
    }

    // --- Disk persistence ---

    [Fact]
    public async Task GetAsync_ReadsFromDisk_WhenNotInMemory()
    {
        CreateSourceFile("/src/spec.md", "content");

        await _sut.SetAsync("/src/spec.md", "Overview", "persisted");

        // Create a new instance (fresh in-memory cache)
        var freshCache = new SectionCache(_fs, NullLogger<SectionCache>.Instance, ProjectRoot);
        var result = await freshCache.GetAsync("/src/spec.md", "Overview");

        Assert.NotNull(result);
        Assert.Equal("persisted", result.Content);
    }

    // --- Corrupted cache entry ---

    [Fact]
    public async Task GetAsync_CorruptedDiskEntry_ReturnsNull()
    {
        CreateSourceFile("/src/spec.md", "content");

        // Write corrupted JSON to cache
        await _sut.SetAsync("/src/spec.md", "Overview", "valid");

        // Now corrupt the disk file by finding it and overwriting
        var cacheDir = StoragePaths.GetSectionsCacheDirectory(ProjectRoot);
        var files = _fs.GetFiles(cacheDir, "*.json").ToList();
        Assert.NotEmpty(files);
        await _fs.WriteAllTextAsync(files[0], "not valid json{{{");

        var freshCache = new SectionCache(_fs, NullLogger<SectionCache>.Instance, ProjectRoot);
        var result = await freshCache.GetAsync("/src/spec.md", "Overview");

        Assert.Null(result); // Silently invalidated
    }

    // --- Different sections same file ---

    [Fact]
    public async Task SetAndGet_DifferentSections_SameFile()
    {
        CreateSourceFile("/src/spec.md", "content");

        await _sut.SetAsync("/src/spec.md", "Intro", "intro content");
        await _sut.SetAsync("/src/spec.md", "Details", "detail content");

        Assert.Equal("intro content", (await _sut.GetAsync("/src/spec.md", "Intro"))!.Content);
        Assert.Equal("detail content", (await _sut.GetAsync("/src/spec.md", "Details"))!.Content);
    }

    // --- Different files same header ---

    [Fact]
    public async Task SetAndGet_SameHeader_DifferentFiles()
    {
        CreateSourceFile("/src/a.md", "a");
        CreateSourceFile("/src/b.md", "b");

        await _sut.SetAsync("/src/a.md", "Overview", "content a");
        await _sut.SetAsync("/src/b.md", "Overview", "content b");

        Assert.Equal("content a", (await _sut.GetAsync("/src/a.md", "Overview"))!.Content);
        Assert.Equal("content b", (await _sut.GetAsync("/src/b.md", "Overview"))!.Content);
    }

    // --- Cache entry metadata ---

    [Fact]
    public async Task SetAsync_RecordsTimestamps()
    {
        CreateSourceFile("/src/spec.md", "content");
        var before = DateTime.UtcNow;

        await _sut.SetAsync("/src/spec.md", "Overview", "cached");

        var result = await _sut.GetAsync("/src/spec.md", "Overview");
        Assert.NotNull(result);
        Assert.True(result.CachedAtUtc >= before);
        Assert.True(result.FileModifiedUtc > DateTime.MinValue);
    }

    // --- IOException during delete is logged ---

    [Fact]
    public async Task InvalidateFileAsync_DeleteThrowsIOException_LogsDebugMessage()
    {
        var inner = new InMemoryFileSystem();
        inner.CreateDirectory(StoragePaths.GetSectionsCacheDirectory(ProjectRoot));
        var throwingFs = new ThrowingDeleteFileSystem(inner);
        var logger = new TestLogger<SectionCache>();
        var cache = new SectionCache(throwingFs, logger, ProjectRoot);

        var dir = Path.GetDirectoryName("/src/spec.md")!;
        if (!inner.DirectoryExists(dir))
            inner.CreateDirectory(dir);
        await inner.WriteAllTextAsync("/src/spec.md", "content");

        await cache.SetAsync("/src/spec.md", "Overview", "cached");

        throwingFs.ThrowOnDelete = true;

        var exception = await Record.ExceptionAsync(() => cache.InvalidateFileAsync("/src/spec.md"));
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
