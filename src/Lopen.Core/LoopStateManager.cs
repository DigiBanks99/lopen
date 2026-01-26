namespace Lopen.Core;

/// <summary>
/// Manages loop state via file-based persistence.
/// </summary>
public class LoopStateManager
{
    private readonly string _workingDirectory;
    private readonly string _doneFilePath;

    /// <summary>
    /// Creates a new LoopStateManager for the current directory.
    /// </summary>
    public LoopStateManager()
        : this(Directory.GetCurrentDirectory())
    {
    }

    /// <summary>
    /// Creates a new LoopStateManager for a specific directory.
    /// </summary>
    public LoopStateManager(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
        _doneFilePath = Path.Combine(_workingDirectory, "lopen.loop.done");
    }

    /// <summary>
    /// Path to the lopen.loop.done file.
    /// </summary>
    public string DoneFilePath => _doneFilePath;

    /// <summary>
    /// Path to the jobs-to-be-done.json file.
    /// </summary>
    public string JobsFilePath => Path.Combine(_workingDirectory, "docs", "requirements", "jobs-to-be-done.json");

    /// <summary>
    /// Path to the IMPLEMENTATION_PLAN.md file.
    /// </summary>
    public string PlanFilePath => Path.Combine(_workingDirectory, "docs", "requirements", "IMPLEMENTATION_PLAN.md");

    /// <summary>
    /// Check if the loop is complete (done file exists).
    /// </summary>
    public bool IsLoopComplete() => File.Exists(_doneFilePath);

    /// <summary>
    /// Remove the done file to restart the loop.
    /// </summary>
    public void RemoveDoneFile()
    {
        if (File.Exists(_doneFilePath))
        {
            File.Delete(_doneFilePath);
        }
    }

    /// <summary>
    /// Create the done file to signal loop completion.
    /// </summary>
    public async Task CreateDoneFileAsync(string? reason = null, CancellationToken ct = default)
    {
        var content = reason ?? $"Loop completed at {DateTime.UtcNow:O}";
        await File.WriteAllTextAsync(_doneFilePath, content, ct);
    }

    /// <summary>
    /// Load the contents of a prompt file.
    /// </summary>
    public async Task<string> LoadPromptAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_workingDirectory, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Prompt file not found: {relativePath}", fullPath);
        }
        return await File.ReadAllTextAsync(fullPath, ct);
    }

    /// <summary>
    /// Check if currently on main branch.
    /// </summary>
    public bool IsOnMainBranch()
    {
        var gitHeadPath = Path.Combine(_workingDirectory, ".git", "HEAD");
        if (!File.Exists(gitHeadPath))
            return false;

        var head = File.ReadAllText(gitHeadPath).Trim();
        return head.EndsWith("/main") || head.EndsWith("/master");
    }
}
