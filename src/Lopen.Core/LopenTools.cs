using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;

namespace Lopen.Core;

/// <summary>
/// Built-in tools for Lopen AI sessions.
/// </summary>
public static class LopenTools
{
    /// <summary>
    /// Tool to read file contents.
    /// </summary>
    public static AIFunction ReadFile() =>
        AIFunctionFactory.Create(ReadFileImpl, "lopen_read_file", "Read the contents of a file");

    /// <summary>
    /// Tool to list directory contents.
    /// </summary>
    public static AIFunction ListDirectory() =>
        AIFunctionFactory.Create(ListDirectoryImpl, "lopen_list_directory", "List files and directories in a path");

    /// <summary>
    /// Tool to get current working directory.
    /// </summary>
    public static AIFunction GetWorkingDirectory() =>
        AIFunctionFactory.Create(GetWorkingDirectoryImpl, "lopen_get_cwd", "Get the current working directory");

    /// <summary>
    /// Tool to check if a file exists.
    /// </summary>
    public static AIFunction FileExists() =>
        AIFunctionFactory.Create(FileExistsImpl, "lopen_file_exists", "Check if a file or directory exists");

    /// <summary>
    /// Tool to get git repository status.
    /// </summary>
    public static AIFunction GitStatus() =>
        AIFunctionFactory.Create(GitStatusImpl, "lopen_git_status", "Get git repository status showing staged, unstaged, and untracked files");

    /// <summary>
    /// Tool to get git diff output.
    /// </summary>
    public static AIFunction GitDiff() =>
        AIFunctionFactory.Create(GitDiffImpl, "lopen_git_diff", "Get git diff for the repository or a specific file");

    /// <summary>
    /// Tool to get recent git commits.
    /// </summary>
    public static AIFunction GitLog() =>
        AIFunctionFactory.Create(GitLogImpl, "lopen_git_log", "Get recent git commit history");

    /// <summary>
    /// Tool to write content to a file.
    /// </summary>
    public static AIFunction WriteFile() =>
        AIFunctionFactory.Create(WriteFileImpl, "lopen_write_file", "Write content to a file, creating it if it doesn't exist");

    /// <summary>
    /// Tool to create a directory.
    /// </summary>
    public static AIFunction CreateDirectory() =>
        AIFunctionFactory.Create(CreateDirectoryImpl, "lopen_create_directory", "Create a directory, including any parent directories");

    /// <summary>
    /// Tool to run a shell command.
    /// </summary>
    public static AIFunction RunCommand() =>
        AIFunctionFactory.Create(RunCommandImpl, "lopen_run_command", "Execute a shell command and return its output");

    /// <summary>
    /// Gets all built-in Lopen tools.
    /// </summary>
    public static ICollection<AIFunction> GetAll() =>
    [
        ReadFile(),
        ListDirectory(),
        GetWorkingDirectory(),
        FileExists(),
        GitStatus(),
        GitDiff(),
        GitLog(),
        WriteFile(),
        CreateDirectory(),
        RunCommand()
    ];

    // Tool implementations

    private static string ReadFileImpl([Description("Path to the file to read")] string path)
    {
        if (string.IsNullOrEmpty(path))
            return "Error: Path cannot be empty";

        try
        {
            if (!File.Exists(path))
                return $"Error: File not found: {path}";

            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return $"Error reading file: {ex.Message}";
        }
    }

    private static string ListDirectoryImpl([Description("Path to the directory to list")] string path)
    {
        if (string.IsNullOrEmpty(path))
            path = Directory.GetCurrentDirectory();

        try
        {
            if (!Directory.Exists(path))
                return $"Error: Directory not found: {path}";

            var entries = Directory.GetFileSystemEntries(path)
                .Select(e => Path.GetFileName(e))
                .OrderBy(e => e);

            return string.Join("\n", entries);
        }
        catch (Exception ex)
        {
            return $"Error listing directory: {ex.Message}";
        }
    }

    private static string GetWorkingDirectoryImpl()
    {
        return Directory.GetCurrentDirectory();
    }

    private static bool FileExistsImpl([Description("Path to check")] string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return File.Exists(path) || Directory.Exists(path);
    }

    private static string GitStatusImpl([Description("Optional path to repository or subdirectory")] string? path = null)
    {
        try
        {
            var workingDir = string.IsNullOrEmpty(path) ? Directory.GetCurrentDirectory() : path;
            return RunGitCommand("status --porcelain", workingDir);
        }
        catch (Exception ex)
        {
            return $"Error getting git status: {ex.Message}";
        }
    }

    private static string GitDiffImpl(
        [Description("Optional file path to get diff for")] string? path = null,
        [Description("Include staged changes")] bool staged = false)
    {
        try
        {
            var args = staged ? "diff --cached" : "diff";
            if (!string.IsNullOrEmpty(path))
                args += $" -- {path}";
            
            return RunGitCommand(args, Directory.GetCurrentDirectory());
        }
        catch (Exception ex)
        {
            return $"Error getting git diff: {ex.Message}";
        }
    }

    private static string GitLogImpl(
        [Description("Number of commits to show")] int limit = 10,
        [Description("Format: oneline, short, medium, full")] string format = "oneline")
    {
        try
        {
            var formatArg = format switch
            {
                "oneline" => "--oneline",
                "short" => "--format=short",
                "medium" => "--format=medium",
                "full" => "--format=full",
                _ => "--oneline"
            };
            
            return RunGitCommand($"log {formatArg} -n {limit}", Directory.GetCurrentDirectory());
        }
        catch (Exception ex)
        {
            return $"Error getting git log: {ex.Message}";
        }
    }

    private static string RunGitCommand(string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
            return "Error: Failed to start git process";

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
            return $"Error: {error}";

        return string.IsNullOrEmpty(output) ? "(no output)" : output;
    }

    private static string WriteFileImpl(
        [Description("Path to the file to write")] string path,
        [Description("Content to write to the file")] string content)
    {
        if (string.IsNullOrEmpty(path))
            return "Error: Path cannot be empty";

        try
        {
            // Ensure parent directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, content);
            return $"Successfully wrote {content.Length} characters to {path}";
        }
        catch (UnauthorizedAccessException)
        {
            return $"Error: Permission denied: {path}";
        }
        catch (Exception ex)
        {
            return $"Error writing file: {ex.Message}";
        }
    }

    private static string CreateDirectoryImpl([Description("Path of the directory to create")] string path)
    {
        if (string.IsNullOrEmpty(path))
            return "Error: Path cannot be empty";

        try
        {
            if (Directory.Exists(path))
                return $"Directory already exists: {path}";

            Directory.CreateDirectory(path);
            return $"Successfully created directory: {path}";
        }
        catch (UnauthorizedAccessException)
        {
            return $"Error: Permission denied: {path}";
        }
        catch (Exception ex)
        {
            return $"Error creating directory: {ex.Message}";
        }
    }

    private static async Task<string> RunCommandImpl(
        [Description("The command to execute")] string command,
        [Description("Working directory (defaults to current directory)")] string? workingDirectory = null,
        [Description("Timeout in seconds (default 30, max 300)")] int timeoutSeconds = 30)
    {
        if (string.IsNullOrEmpty(command))
            return "Error: Command cannot be empty";

        // Cap timeout at 5 minutes for safety
        timeoutSeconds = Math.Min(Math.Max(timeoutSeconds, 1), 300);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
                Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
                WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return "Error: Failed to start process";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
                var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

                await process.WaitForExitAsync(cts.Token);

                var output = await outputTask;
                var error = await errorTask;

                var result = new System.Text.StringBuilder();
                if (!string.IsNullOrEmpty(output))
                    result.AppendLine(output.TrimEnd());
                if (!string.IsNullOrEmpty(error))
                    result.AppendLine($"[stderr]\n{error.TrimEnd()}");
                
                result.AppendLine($"[exit code: {process.ExitCode}]");

                return result.ToString();
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return $"Error: Command timed out after {timeoutSeconds} seconds";
            }
        }
        catch (Exception ex)
        {
            return $"Error executing command: {ex.Message}";
        }
    }
}
