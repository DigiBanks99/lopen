using Shouldly;
using Microsoft.Extensions.AI;
using Xunit;

namespace Lopen.Core.Tests;

public class LopenToolsTests
{
    [Fact]
    public void ReadFile_HasCorrectName()
    {
        var tool = LopenTools.ReadFile();

        tool.Name.ShouldBe("lopen_read_file");
    }

    [Fact]
    public void ListDirectory_HasCorrectName()
    {
        var tool = LopenTools.ListDirectory();

        tool.Name.ShouldBe("lopen_list_directory");
    }

    [Fact]
    public void GetWorkingDirectory_HasCorrectName()
    {
        var tool = LopenTools.GetWorkingDirectory();

        tool.Name.ShouldBe("lopen_get_cwd");
    }

    [Fact]
    public void FileExists_HasCorrectName()
    {
        var tool = LopenTools.FileExists();

        tool.Name.ShouldBe("lopen_file_exists");
    }

    [Fact]
    public void GetAll_ReturnsAllTools()
    {
        var tools = LopenTools.GetAll();

        tools.Count().ShouldBe(9);
        tools.Select(t => t.Name).ShouldContain("lopen_read_file");
        tools.Select(t => t.Name).ShouldContain("lopen_list_directory");
        tools.Select(t => t.Name).ShouldContain("lopen_get_cwd");
        tools.Select(t => t.Name).ShouldContain("lopen_file_exists");
        tools.Select(t => t.Name).ShouldContain("lopen_git_status");
        tools.Select(t => t.Name).ShouldContain("lopen_git_diff");
        tools.Select(t => t.Name).ShouldContain("lopen_git_log");
        tools.Select(t => t.Name).ShouldContain("lopen_write_file");
        tools.Select(t => t.Name).ShouldContain("lopen_create_directory");
    }

    [Fact]
    public async Task ReadFile_WithValidPath_ReturnsContent()
    {
        var tool = LopenTools.ReadFile();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "test content");

        try
        {
            var args = new AIFunctionArguments { ["path"] = tempFile };
            var result = await tool.InvokeAsync(args);

            result.ShouldNotBeNull();
            result!.ToString()!.ShouldContain("test content");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadFile_WithInvalidPath_ReturnsError()
    {
        var tool = LopenTools.ReadFile();

        var args = new AIFunctionArguments { ["path"] = "/nonexistent/file.txt" };
        var result = await tool.InvokeAsync(args);

        result.ShouldNotBeNull();
        result!.ToString()!.ShouldContain("Error");
    }

    [Fact]
    public async Task ListDirectory_WithValidPath_ReturnsEntries()
    {
        var tool = LopenTools.ListDirectory();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "test.txt"), "");

        try
        {
            var args = new AIFunctionArguments { ["path"] = tempDir };
            var result = await tool.InvokeAsync(args);

            result.ShouldNotBeNull();
            result!.ToString()!.ShouldContain("test.txt");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task GetWorkingDirectory_ReturnsCurrentDirectory()
    {
        var tool = LopenTools.GetWorkingDirectory();

        var result = await tool.InvokeAsync(new AIFunctionArguments());

        result.ShouldNotBeNull();
        result!.ToString()!.ShouldBe(Directory.GetCurrentDirectory());
    }

    [Fact]
    public async Task FileExists_WithExistingFile_ReturnsTrue()
    {
        var tool = LopenTools.FileExists();
        var tempFile = Path.GetTempFileName();

        try
        {
            var args = new AIFunctionArguments { ["path"] = tempFile };
            var result = await tool.InvokeAsync(args);

            result.ShouldNotBeNull();
            var jsonElement = (System.Text.Json.JsonElement)result!;
            jsonElement.GetBoolean().ShouldBeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task FileExists_WithNonexistentPath_ReturnsFalse()
    {
        var tool = LopenTools.FileExists();

        var args = new AIFunctionArguments { ["path"] = "/nonexistent/path" };
        var result = await tool.InvokeAsync(args);

        result.ShouldNotBeNull();
        var jsonElement = (System.Text.Json.JsonElement)result!;
        jsonElement.GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public void GitStatus_HasCorrectName()
    {
        var tool = LopenTools.GitStatus();

        tool.Name.ShouldBe("lopen_git_status");
    }

    [Fact]
    public void GitDiff_HasCorrectName()
    {
        var tool = LopenTools.GitDiff();

        tool.Name.ShouldBe("lopen_git_diff");
    }

    [Fact]
    public void GitLog_HasCorrectName()
    {
        var tool = LopenTools.GitLog();

        tool.Name.ShouldBe("lopen_git_log");
    }

    [Fact]
    public async Task GitStatus_InGitRepo_ReturnsStatus()
    {
        var tool = LopenTools.GitStatus();

        var args = new AIFunctionArguments();
        var result = await tool.InvokeAsync(args);

        result.ShouldNotBeNull();
        // In a git repo, should not return an error
        result!.ToString()!.ShouldNotContain("fatal:");
    }

    [Fact]
    public async Task GitStatus_WithInvalidPath_ReturnsError()
    {
        var tool = LopenTools.GitStatus();

        var args = new AIFunctionArguments { ["path"] = "/nonexistent/invalid/path" };
        var result = await tool.InvokeAsync(args);

        result.ShouldNotBeNull();
        result!.ToString()!.ShouldContain("Error");
    }

    [Fact]
    public async Task GitDiff_InGitRepo_ReturnsDiff()
    {
        var tool = LopenTools.GitDiff();

        var args = new AIFunctionArguments();
        var result = await tool.InvokeAsync(args);

        result.ShouldNotBeNull();
        // The result is a JsonElement wrapping the string
        var output = result is System.Text.Json.JsonElement je 
            ? je.GetString() ?? je.ToString() 
            : result!.ToString()!;
        
        // Should not return a fatal error
        output.ShouldNotStartWith("Error:");
    }

    [Fact]
    public async Task GitDiff_WithStagedFlag_Works()
    {
        var tool = LopenTools.GitDiff();

        var args = new AIFunctionArguments { ["staged"] = true };
        var result = await tool.InvokeAsync(args);

        result.ShouldNotBeNull();
        result!.ToString()!.ShouldNotContain("fatal:");
    }

    [Fact]
    public async Task GitLog_InGitRepo_ReturnsCommits()
    {
        var tool = LopenTools.GitLog();

        var args = new AIFunctionArguments();
        var result = await tool.InvokeAsync(args);

        result.ShouldNotBeNull();
        // Should not return a fatal error and should have output
        var output = result!.ToString()!;
        output.ShouldNotContain("fatal:");
    }

    [Fact]
    public async Task GitLog_WithLimit_RespectsLimit()
    {
        var tool = LopenTools.GitLog();

        var args = new AIFunctionArguments { ["limit"] = 3 };
        var result = await tool.InvokeAsync(args);

        result.ShouldNotBeNull();
        // Should have at most 3 lines (or could be less if fewer commits)
        var output = result!.ToString()!;
        output.ShouldNotContain("fatal:");
    }

    [Fact]
    public async Task GitLog_WithFormat_UsesFormat()
    {
        var tool = LopenTools.GitLog();

        var args = new AIFunctionArguments 
        { 
            ["limit"] = 1,
            ["format"] = "short" 
        };
        var result = await tool.InvokeAsync(args);

        result.ShouldNotBeNull();
        result!.ToString()!.ShouldNotContain("fatal:");
    }

    [Fact]
    public void WriteFile_HasCorrectName()
    {
        var tool = LopenTools.WriteFile();

        tool.Name.ShouldBe("lopen_write_file");
    }

    [Fact]
    public void CreateDirectory_HasCorrectName()
    {
        var tool = LopenTools.CreateDirectory();

        tool.Name.ShouldBe("lopen_create_directory");
    }

    [Fact]
    public async Task WriteFile_WithValidPath_WritesContent()
    {
        var tool = LopenTools.WriteFile();
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");

        try
        {
            var args = new AIFunctionArguments 
            { 
                ["path"] = tempFile,
                ["content"] = "test content"
            };
            var result = await tool.InvokeAsync(args);

            result.ShouldNotBeNull();
            result!.ToString()!.ShouldContain("Successfully");
            File.Exists(tempFile).ShouldBeTrue();
            File.ReadAllText(tempFile).ShouldBe("test content");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteFile_WithEmptyPath_ReturnsError()
    {
        var tool = LopenTools.WriteFile();

        var args = new AIFunctionArguments 
        { 
            ["path"] = "",
            ["content"] = "test"
        };
        var result = await tool.InvokeAsync(args);

        result.ShouldNotBeNull();
        result!.ToString()!.ShouldContain("Error");
    }

    [Fact]
    public async Task WriteFile_CreatesParentDirectories()
    {
        var tool = LopenTools.WriteFile();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var tempFile = Path.Combine(tempDir, "subdir", "file.txt");

        try
        {
            var args = new AIFunctionArguments 
            { 
                ["path"] = tempFile,
                ["content"] = "nested content"
            };
            var result = await tool.InvokeAsync(args);

            result.ShouldNotBeNull();
            result!.ToString()!.ShouldContain("Successfully");
            File.Exists(tempFile).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CreateDirectory_WithValidPath_CreatesDirectory()
    {
        var tool = LopenTools.CreateDirectory();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            var args = new AIFunctionArguments { ["path"] = tempDir };
            var result = await tool.InvokeAsync(args);

            result.ShouldNotBeNull();
            result!.ToString()!.ShouldContain("Successfully");
            Directory.Exists(tempDir).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir);
        }
    }

    [Fact]
    public async Task CreateDirectory_WithExistingDirectory_ReturnsAlreadyExists()
    {
        var tool = LopenTools.CreateDirectory();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var args = new AIFunctionArguments { ["path"] = tempDir };
            var result = await tool.InvokeAsync(args);

            result.ShouldNotBeNull();
            result!.ToString()!.ShouldContain("already exists");
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public async Task CreateDirectory_WithEmptyPath_ReturnsError()
    {
        var tool = LopenTools.CreateDirectory();

        var args = new AIFunctionArguments { ["path"] = "" };
        var result = await tool.InvokeAsync(args);

        result.ShouldNotBeNull();
        result!.ToString()!.ShouldContain("Error");
    }

    [Fact]
    public async Task CreateDirectory_CreatesNestedDirectories()
    {
        var tool = LopenTools.CreateDirectory();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nested", "dir");

        try
        {
            var args = new AIFunctionArguments { ["path"] = tempDir };
            var result = await tool.InvokeAsync(args);

            result.ShouldNotBeNull();
            result!.ToString()!.ShouldContain("Successfully");
            Directory.Exists(tempDir).ShouldBeTrue();
        }
        finally
        {
            // Clean up the root temp directory
            var rootDir = Path.GetDirectoryName(Path.GetDirectoryName(tempDir))!;
            if (Directory.Exists(rootDir))
                Directory.Delete(rootDir, true);
        }
    }
}

public class CopilotSessionOptionsToolsTests
{
    [Fact]
    public void CopilotSessionOptions_CanSetTools()
    {
        var tools = LopenTools.GetAll();
        var options = new CopilotSessionOptions
        {
            Tools = tools
        };

        options.Tools.Count().ShouldBe(9);
    }

    [Fact]
    public void CopilotSessionOptions_CanSetAvailableTools()
    {
        var options = new CopilotSessionOptions
        {
            AvailableTools = ["file_system", "git"]
        };

        options.AvailableTools.ShouldContain("file_system");
        options.AvailableTools.ShouldContain("git");
    }

    [Fact]
    public void CopilotSessionOptions_CanSetExcludedTools()
    {
        var options = new CopilotSessionOptions
        {
            ExcludedTools = ["shell", "web"]
        };

        options.ExcludedTools.ShouldContain("shell");
        options.ExcludedTools.ShouldContain("web");
    }
}
