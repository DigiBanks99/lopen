using Lopen.Storage;

namespace Lopen.Llm.Tests.Tools;

/// <summary>
/// In-memory file system for tool operation tests.
/// </summary>
internal sealed class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);

    public void AddFile(string path, string content)
    {
        _files[Normalize(path)] = content;
        // Ensure parent directory exists
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            _directories.Add(Normalize(dir));
    }

    public void AddDirectory(string path) => _directories.Add(Normalize(path));

    public void CreateDirectory(string path) => _directories.Add(Normalize(path));

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public bool DirectoryExists(string path) =>
        _directories.Contains(Normalize(path));

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var key = Normalize(path);
        if (!_files.TryGetValue(key, out var content))
            throw new FileNotFoundException($"File not found: {path}", path);
        return Task.FromResult(content);
    }

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        _files[Normalize(path)] = content;
        return Task.CompletedTask;
    }

    public IEnumerable<string> GetFiles(string path, string searchPattern = "*")
    {
        var prefix = Normalize(path);
        if (!prefix.EndsWith('/')) prefix += "/";

        return _files.Keys
            .Where(f => f.StartsWith(prefix, StringComparison.Ordinal)
                        && !f[prefix.Length..].Contains('/'))
            .Where(f => MatchesPattern(Path.GetFileName(f), searchPattern));
    }

    public IEnumerable<string> GetDirectories(string path)
    {
        var prefix = Normalize(path);
        if (!prefix.EndsWith('/')) prefix += "/";
        return _directories
            .Where(d => d.StartsWith(prefix, StringComparison.Ordinal)
                        && d != prefix.TrimEnd('/')
                        && !d[prefix.Length..].Contains('/'));
    }

    public void MoveFile(string source, string dest)
    {
        var src = Normalize(source);
        if (!_files.TryGetValue(src, out var content))
            throw new FileNotFoundException(source);
        _files[Normalize(dest)] = content;
        _files.Remove(src);
    }

    public void DeleteFile(string path) => _files.Remove(Normalize(path));

    public void CreateSymlink(string linkPath, string targetPath) { }

    public string? GetSymlinkTarget(string linkPath) => null;

    public void DeleteDirectory(string path, bool recursive = true)
    {
        var normalized = Normalize(path);
        _directories.Remove(normalized);
        if (recursive)
        {
            var prefix = normalized + "/";
            foreach (var f in _files.Keys.Where(k => k.StartsWith(prefix)).ToList())
                _files.Remove(f);
            foreach (var d in _directories.Where(k => k.StartsWith(prefix)).ToList())
                _directories.Remove(d);
        }
    }

    public DateTime GetLastWriteTimeUtc(string path) => DateTime.MinValue;

    public string? GetContent(string path) =>
        _files.TryGetValue(Normalize(path), out var c) ? c : null;

    private static string Normalize(string path) =>
        path.Replace('\\', '/').TrimEnd('/');

    private static bool MatchesPattern(string fileName, string pattern)
    {
        if (pattern == "*") return true;
        // Simple wildcard: RESEARCH-*.md → starts with "RESEARCH-" and ends with ".md"
        if (pattern.Contains('*'))
        {
            var parts = pattern.Split('*', 2);
            return fileName.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase)
                   && fileName.EndsWith(parts[1], StringComparison.OrdinalIgnoreCase);
        }
        return string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase);
    }
}
