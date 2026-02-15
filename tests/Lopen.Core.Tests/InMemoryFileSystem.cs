using Lopen.Storage;

namespace Lopen.Core.Tests;

/// <summary>
/// Minimal in-memory file system for Core module tests.
/// </summary>
internal sealed class InMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    public void AddFile(string path, string content)
    {
        _files[Normalize(path)] = content;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            _directories.Add(Normalize(dir));
    }

    public void AddDirectory(string path) => _directories.Add(Normalize(path));

    public void CreateDirectory(string path) => _directories.Add(Normalize(path));
    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));
    public bool DirectoryExists(string path) => _directories.Contains(Normalize(path));

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
        _files.TryGetValue(Normalize(path), out var content)
            ? Task.FromResult(content)
            : throw new FileNotFoundException("File not found", path);

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        _files[Normalize(path)] = content;
        return Task.CompletedTask;
    }

    public IEnumerable<string> GetFiles(string path, string searchPattern = "*") =>
        _files.Keys.Where(f => f.StartsWith(Normalize(path), StringComparison.OrdinalIgnoreCase));

    public IEnumerable<string> GetDirectories(string path) =>
        _directories.Where(d =>
        {
            var normalized = Normalize(path);
            if (!d.StartsWith(normalized, StringComparison.OrdinalIgnoreCase) || d == normalized)
                return false;
            var remainder = d[(normalized.Length + 1)..];
            return !remainder.Contains('/');
        });

    public void MoveFile(string sourcePath, string destinationPath)
    {
        if (_files.Remove(Normalize(sourcePath), out var content))
            _files[Normalize(destinationPath)] = content;
    }

    public void DeleteFile(string path) => _files.Remove(Normalize(path));
    public void CreateSymlink(string linkPath, string targetPath) { }
    public string? GetSymlinkTarget(string linkPath) => null;
    public DateTime GetLastWriteTimeUtc(string path) => DateTime.UtcNow;

    private static string Normalize(string path) => path.Replace('\\', '/').TrimEnd('/');
}
