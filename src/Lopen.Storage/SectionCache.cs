using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Lopen.Storage;

/// <summary>
/// Dual-layer section cache (in-memory + disk) keyed by file path + section header.
/// Invalidated when the source file's modification timestamp changes.
/// Corrupted cache entries are silently invalidated and regenerated.
/// </summary>
public sealed class SectionCache : ISectionCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
    };

    private readonly ConcurrentDictionary<string, SectionCacheEntry> _memory = new(StringComparer.Ordinal);
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<SectionCache> _logger;
    private readonly string _cacheDirectory;

    public SectionCache(IFileSystem fileSystem, ILogger<SectionCache> logger, string projectRoot)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        _fileSystem = fileSystem;
        _logger = logger;
        _cacheDirectory = StoragePaths.GetSectionsCacheDirectory(projectRoot);
    }

    public async Task<SectionCacheEntry?> GetAsync(string filePath, string sectionHeader, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionHeader);

        var key = BuildKey(filePath, sectionHeader);

        // Check in-memory first
        if (_memory.TryGetValue(key, out var cached))
        {
            if (IsValid(filePath, cached))
                return cached;

            _memory.TryRemove(key, out _);
        }

        // Check disk
        var diskPath = GetDiskPath(key);
        if (!_fileSystem.FileExists(diskPath))
            return null;

        try
        {
            var json = await _fileSystem.ReadAllTextAsync(diskPath, cancellationToken);
            var entry = JsonSerializer.Deserialize<SectionCacheEntry>(json, JsonOptions);

            if (entry is not null && IsValid(filePath, entry))
            {
                _memory[key] = entry;
                return entry;
            }

            // Stale or corrupted - remove silently
            _fileSystem.DeleteFile(diskPath);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogDebug(ex, "Corrupted section cache entry for {FilePath}:{Header}, invalidating", filePath, sectionHeader);
            TryDeleteFile(diskPath);
        }

        return null;
    }

    public async Task SetAsync(string filePath, string sectionHeader, string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionHeader);
        ArgumentNullException.ThrowIfNull(content);

        var fileModified = _fileSystem.GetLastWriteTimeUtc(filePath);
        var entry = new SectionCacheEntry
        {
            Content = content,
            FileModifiedUtc = fileModified,
            CachedAtUtc = DateTime.UtcNow,
        };

        var key = BuildKey(filePath, sectionHeader);
        _memory[key] = entry;

        // Persist to disk
        try
        {
            if (!_fileSystem.DirectoryExists(_cacheDirectory))
                _fileSystem.CreateDirectory(_cacheDirectory);

            var diskPath = GetDiskPath(key);
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            var tempPath = diskPath + ".tmp";
            await _fileSystem.WriteAllTextAsync(tempPath, json, cancellationToken);
            _fileSystem.MoveFile(tempPath, diskPath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to persist section cache entry for {FilePath}:{Header}", filePath, sectionHeader);
        }
    }

    public Task InvalidateFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Remove all in-memory entries for this file
        var keysToRemove = _memory.Keys
            .Where(k => k.StartsWith(filePath + "::", StringComparison.Ordinal))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _memory.TryRemove(key, out _);
            TryDeleteFile(GetDiskPath(key));
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _memory.Clear();

        if (_fileSystem.DirectoryExists(_cacheDirectory))
        {
            foreach (var file in _fileSystem.GetFiles(_cacheDirectory, "*.json"))
            {
                TryDeleteFile(file);
            }
        }

        return Task.CompletedTask;
    }

    private bool IsValid(string filePath, SectionCacheEntry entry)
    {
        var currentModified = _fileSystem.GetLastWriteTimeUtc(filePath);
        return currentModified == entry.FileModifiedUtc;
    }

    private static string BuildKey(string filePath, string sectionHeader) =>
        $"{filePath}::{sectionHeader}";

    private string GetDiskPath(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];
        return Path.Combine(_cacheDirectory, $"{hash}.json");
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (_fileSystem.FileExists(path))
                _fileSystem.DeleteFile(path);
        }
        catch (IOException)
        {
            // Best effort cleanup
        }
    }
}
