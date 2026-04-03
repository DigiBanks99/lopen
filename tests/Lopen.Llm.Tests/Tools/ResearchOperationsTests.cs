using Lopen.Llm.Tools;
using System.Text.Json;

namespace Lopen.Llm.Tests.Tools;

public sealed class ResearchOperationsTests
{
    private const string ProjectRoot = "/project";

    private static string ResearchDir(string module) =>
        $"/project/docs/requirements/{module}";

    [Fact]
    public async Task ReadResearch_ReturnsContent_WhenTopicExists()
    {
        var fs = new FakeFileSystem();
        var dir = ResearchDir("auth");
        fs.AddDirectory(dir);
        fs.AddFile($"{dir}/RESEARCH-caching.md", "# Caching research");

        var result = await ResearchOperations.ReadResearch(fs, ProjectRoot, "auth", "caching");

        Assert.Equal("# Caching research", result);
    }

    [Fact]
    public async Task ReadResearch_ReturnsError_WhenDirectoryNotFound()
    {
        var fs = new FakeFileSystem();

        var result = await ResearchOperations.ReadResearch(fs, ProjectRoot, "missing", "topic");

        var doc = JsonDocument.Parse(result);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Contains("not found", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ReadResearch_ReturnsError_WhenTopicNotFound()
    {
        var fs = new FakeFileSystem();
        fs.AddDirectory(ResearchDir("auth"));

        var result = await ResearchOperations.ReadResearch(fs, ProjectRoot, "auth", "nonexistent");

        var doc = JsonDocument.Parse(result);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Contains("RESEARCH-nonexistent.md", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ReadResearch_ReturnsMainResearch_WhenNoTopicSpecified()
    {
        var fs = new FakeFileSystem();
        var dir = ResearchDir("auth");
        fs.AddDirectory(dir);
        fs.AddFile($"{dir}/RESEARCH.md", "# Research Index\n- stuff");

        var result = await ResearchOperations.ReadResearch(fs, ProjectRoot, "auth", null);

        Assert.Equal("# Research Index\n- stuff", result);
    }

    [Fact]
    public async Task LogResearch_WritesFile_AndUpdatesIndex()
    {
        var fs = new FakeFileSystem();

        var result = await ResearchOperations.LogResearch(
            fs, ProjectRoot, "auth", "caching", "# Caching notes\nSome findings");

        var doc = JsonDocument.Parse(result);
        Assert.Equal("success", doc.RootElement.GetProperty("status").GetString());

        // Verify file was written
        var filePath = $"{ResearchDir("auth")}/RESEARCH-caching.md";
        Assert.True(fs.FileExists(filePath));
        Assert.Equal("# Caching notes\nSome findings", fs.GetContent(filePath));

        // Verify index was updated
        var indexPath = $"{ResearchDir("auth")}/RESEARCH.md";
        Assert.True(fs.FileExists(indexPath));
        var index = fs.GetContent(indexPath)!;
        Assert.Contains("caching", index);
    }

    [Fact]
    public async Task LogResearch_ReturnsError_WhenContentEmpty()
    {
        var fs = new FakeFileSystem();

        var result = await ResearchOperations.LogResearch(fs, ProjectRoot, "auth", "topic", "");

        var doc = JsonDocument.Parse(result);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Contains("required", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public void SanitizeTopicSlug_ReplacesSpecialChars()
    {
        var result = ResearchOperations.SanitizeTopicSlug("hello world!");

        Assert.Equal("hello-world", result);
        Assert.DoesNotContain(" ", result);
        Assert.DoesNotContain("!", result);
    }

    [Fact]
    public void SanitizeTopicSlug_CollapsesMultipleHyphens()
    {
        var result = ResearchOperations.SanitizeTopicSlug("a---b");

        Assert.Equal("a-b", result);
    }
}
