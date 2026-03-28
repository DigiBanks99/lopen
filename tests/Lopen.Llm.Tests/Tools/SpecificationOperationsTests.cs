using System.Text.Json;
using Lopen.Llm.Tools;

namespace Lopen.Llm.Tests.Tools;

public sealed class SpecificationOperationsTests
{
    private const string ProjectRoot = "/project";

    private static string SpecPath(string module) =>
        $"/project/docs/requirements/{module}/SPECIFICATION.md";

    private static FakeFileSystem FsWithSpec(string module, string content)
    {
        var fs = new FakeFileSystem();
        fs.AddFile(SpecPath(module), content);
        return fs;
    }

    private static IToolSectionExtractor StubExtractor(
        Func<string, IReadOnlyList<string>, IReadOnlyList<ToolExtractedSection>> func) =>
        new DelegateExtractor(func);

    private static IToolSectionExtractor PassthroughExtractor() =>
        StubExtractor((_, _) => Array.Empty<ToolExtractedSection>());

    [Fact]
    public async Task ReadSpec_ReturnsContent_WhenSpecExists()
    {
        var fs = FsWithSpec("auth", "# Auth Spec\nSome content");
        var extractor = PassthroughExtractor();

        var result = await SpecificationOperations.ReadSpec(fs, extractor, ProjectRoot, "auth", null);

        Assert.Equal("# Auth Spec\nSome content", result);
    }

    [Fact]
    public async Task ReadSpec_ReturnsError_WhenSpecNotFound()
    {
        var fs = new FakeFileSystem();
        var extractor = PassthroughExtractor();

        var result = await SpecificationOperations.ReadSpec(fs, extractor, ProjectRoot, "missing", null);

        var doc = JsonDocument.Parse(result);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Contains("not found", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ReadSpec_ExtractsSection_WhenSectionProvided()
    {
        var fs = FsWithSpec("auth", "# Auth\n## Overview\nOverview text\n## Details\nDetails text");
        var extractor = StubExtractor((content, headers) =>
            new List<ToolExtractedSection>
            {
                new("Overview", "Overview text")
            });

        var result = await SpecificationOperations.ReadSpec(fs, extractor, ProjectRoot, "auth", "Overview");

        Assert.Equal("Overview text", result);
    }

    [Fact]
    public async Task ReadSpec_ReturnsError_WhenSectionNotFound()
    {
        var fs = FsWithSpec("auth", "# Auth\n## Overview\nSome text");
        var extractor = StubExtractor((_, _) => Array.Empty<ToolExtractedSection>());

        var result = await SpecificationOperations.ReadSpec(fs, extractor, ProjectRoot, "auth", "NonExistent");

        var doc = JsonDocument.Parse(result);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Contains("NonExistent", doc.RootElement.GetProperty("message").GetString());
    }

    private sealed class DelegateExtractor(
        Func<string, IReadOnlyList<string>, IReadOnlyList<ToolExtractedSection>> func)
        : IToolSectionExtractor
    {
        public IReadOnlyList<ToolExtractedSection> ExtractRelevantSections(
            string content, IReadOnlyList<string> headers) => func(content, headers);
    }
}
