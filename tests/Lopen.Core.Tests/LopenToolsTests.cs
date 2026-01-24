using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Lopen.Core.Tests;

public class LopenToolsTests
{
    [Fact]
    public void ReadFile_HasCorrectName()
    {
        var tool = LopenTools.ReadFile();

        tool.Name.Should().Be("lopen_read_file");
    }

    [Fact]
    public void ListDirectory_HasCorrectName()
    {
        var tool = LopenTools.ListDirectory();

        tool.Name.Should().Be("lopen_list_directory");
    }

    [Fact]
    public void GetWorkingDirectory_HasCorrectName()
    {
        var tool = LopenTools.GetWorkingDirectory();

        tool.Name.Should().Be("lopen_get_cwd");
    }

    [Fact]
    public void FileExists_HasCorrectName()
    {
        var tool = LopenTools.FileExists();

        tool.Name.Should().Be("lopen_file_exists");
    }

    [Fact]
    public void GetAll_ReturnsAllTools()
    {
        var tools = LopenTools.GetAll();

        tools.Should().HaveCount(4);
        tools.Select(t => t.Name).Should().Contain("lopen_read_file");
        tools.Select(t => t.Name).Should().Contain("lopen_list_directory");
        tools.Select(t => t.Name).Should().Contain("lopen_get_cwd");
        tools.Select(t => t.Name).Should().Contain("lopen_file_exists");
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

            result.Should().NotBeNull();
            result!.ToString().Should().Contain("test content");
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

        result.Should().NotBeNull();
        result!.ToString().Should().Contain("Error");
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

            result.Should().NotBeNull();
            result!.ToString().Should().Contain("test.txt");
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

        result.Should().NotBeNull();
        result!.ToString().Should().Be(Directory.GetCurrentDirectory());
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

            result.Should().NotBeNull();
            var jsonElement = (System.Text.Json.JsonElement)result!;
            jsonElement.GetBoolean().Should().BeTrue();
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

        result.Should().NotBeNull();
        var jsonElement = (System.Text.Json.JsonElement)result!;
        jsonElement.GetBoolean().Should().BeFalse();
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

        options.Tools.Should().HaveCount(4);
    }

    [Fact]
    public void CopilotSessionOptions_CanSetAvailableTools()
    {
        var options = new CopilotSessionOptions
        {
            AvailableTools = ["file_system", "git"]
        };

        options.AvailableTools.Should().Contain("file_system");
        options.AvailableTools.Should().Contain("git");
    }

    [Fact]
    public void CopilotSessionOptions_CanSetExcludedTools()
    {
        var options = new CopilotSessionOptions
        {
            ExcludedTools = ["shell", "web"]
        };

        options.ExcludedTools.Should().Contain("shell");
        options.ExcludedTools.Should().Contain("web");
    }
}
