using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Lopen.Storage;

/// <summary>
/// Caches assessment results with scope-based invalidation.
/// Invalidated when any file in the assessed scope has changed.
/// Corrupted cache entries are silently invalidated and regenerated.
/// </summary>
public sealed class AssessmentCache : IAssessmentCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
    };

    private readonly IFileSystem _fileSystem;
    private readonly ILogger<AssessmentCache> _logger;
    private readonly string _cacheDirectory;

    public AssessmentCache(IFileSystem fileSystem, ILogger<AssessmentCache> logger, string projectRoot)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        _fileSystem = fileSystem;
        _logger = logger;
        _cacheDirectory = StoragePaths.GetAssessmentsCacheDirectory(projectRoot);
    }

    public async Task<AssessmentCacheEntry?> GetAsync(string scopeKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);

        var diskPath = GetDiskPath(scopeKey);
        if (!_fileSystem.FileExists(diskPath))
            return null;

        try
        {
            var json = await _fileSystem.ReadAllTextAsync(diskPath, cancellationToken);
            var entry = JsonSerializer.Deserialize<AssessmentCacheEntry>(json, JsonOptions);

            if (entry is not null && IsValid(entry))
                return entry;

            // Stale - remove silently
            _fileSystem.DeleteFile(diskPath);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogDebug(ex, "Corrupted assessment cache entry for scope {ScopeKey}, invalidating", scopeKey);
            TryDeleteFile(diskPath);
        }

        return null;
    }

    public async Task SetAsync(string scopeKey, string content, IReadOnlyDictionary<string, DateTime> fileTimestamps, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fileTimestamps);

        var entry = new AssessmentCacheEntry
        {
            Content = content,
            CachedAtUtc = DateTime.UtcNow,
            FileTimestamps = fileTimestamps,
        };

        try
        {
            if (!_fileSystem.DirectoryExists(_cacheDirectory))
                _fileSystem.CreateDirectory(_cacheDirectory);

            var diskPath = GetDiskPath(scopeKey);
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            var tempPath = diskPath + ".tmp";
            await _fileSystem.WriteAllTextAsync(tempPath, json, cancellationToken);
            _fileSystem.MoveFile(tempPath, diskPath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to persist assessment cache entry for scope {ScopeKey}", scopeKey);
        }
    }

    public Task InvalidateAsync(string scopeKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);

        var diskPath = GetDiskPath(scopeKey);
        TryDeleteFile(diskPath);

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (_fileSystem.DirectoryExists(_cacheDirectory))
        {
            foreach (var file in _fileSystem.GetFiles(_cacheDirectory, "*.json"))
            {
                TryDeleteFile(file);
            }
        }

        return Task.CompletedTask;
    }

    private bool IsValid(AssessmentCacheEntry entry)
    {
        foreach (var (filePath, cachedTimestamp) in entry.FileTimestamps)
        {
            var currentTimestamp = _fileSystem.GetLastWriteTimeUtc(filePath);
            if (currentTimestamp != cachedTimestamp)
                return false;
        }

        return true;
    }

    private string GetDiskPath(string scopeKey)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scopeKey)))[..16];
        return Path.Combine(_cacheDirectory, $"{hash}.json");
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (_fileSystem.FileExists(path))
                _fileSystem.DeleteFile(path);
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Best-effort cache cleanup failed for {Path}", path);
        }
    }
}
