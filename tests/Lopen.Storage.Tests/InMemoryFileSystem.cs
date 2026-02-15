namespace Lopen.Storage.Tests;

/// <summary>
/// In-memory file system implementation for testing.
/// </summary>
internal sealed class InMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _symlinks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> _lastWriteTimes = new(StringComparer.Ordinal);

    public void CreateDirectory(string path)
    {
        _directories.Add(NormalizePath(path));
    }

    public bool FileExists(string path) =>
        _files.ContainsKey(NormalizePath(path));

    public bool DirectoryExists(string path) =>
        _directories.Contains(NormalizePath(path)) ||
        _symlinks.ContainsKey(NormalizePath(path));

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizePath(path);
        if (!_files.TryGetValue(normalized, out var content))
        {
            throw new FileNotFoundException($"File not found: {path}", path);
        }
        return Task.FromResult(content);
    }

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizePath(path);
        _files[normalized] = content;
        _lastWriteTimes[normalized] = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public IEnumerable<string> GetFiles(string path, string searchPattern = "*")
    {
        var normalized = NormalizePath(path);
        var prefix = normalized.EndsWith('/') ? normalized : normalized + "/";
        return _files.Keys
            .Where(f => f.StartsWith(prefix, StringComparison.Ordinal) &&
                        !f[prefix.Length..].Contains('/'));
    }

    public IEnumerable<string> GetDirectories(string path)
    {
        var normalized = NormalizePath(path);
        var prefix = normalized.EndsWith('/') ? normalized : normalized + "/";
        return _directories
            .Where(d => d.StartsWith(prefix, StringComparison.Ordinal) &&
                        d != normalized &&
                        !d[prefix.Length..].Contains('/'))
            .Concat(_symlinks.Keys.Where(d => d.StartsWith(prefix, StringComparison.Ordinal) &&
                        !d[prefix.Length..].Contains('/')))
            .Distinct();
    }

    public void MoveFile(string sourcePath, string destinationPath)
    {
        var src = NormalizePath(sourcePath);
        var dst = NormalizePath(destinationPath);

        if (!_files.TryGetValue(src, out var content))
        {
            throw new FileNotFoundException($"Source file not found: {sourcePath}", sourcePath);
        }

        _files[dst] = content;
        _files.Remove(src);
    }

    public void DeleteFile(string path)
    {
        var normalized = NormalizePath(path);
        _files.Remove(normalized);
        _symlinks.Remove(normalized);
        _directories.Remove(normalized);
    }

    public void CreateSymlink(string linkPath, string targetPath)
    {
        var normalized = NormalizePath(linkPath);
        _symlinks[normalized] = targetPath;
    }

    public string? GetSymlinkTarget(string linkPath)
    {
        var normalized = NormalizePath(linkPath);
        return _symlinks.TryGetValue(normalized, out var target) ? target : null;
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        var normalized = NormalizePath(path);
        return _lastWriteTimes.TryGetValue(normalized, out var time) ? time : DateTime.MinValue;
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').TrimEnd('/');
}
