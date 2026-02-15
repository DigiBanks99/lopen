using Lopen.Core.Documents;

namespace Lopen.Core.Tests.Documents;

public class MarkdigSpecificationParserTests
{
    private readonly MarkdigSpecificationParser _parser = new();

    [Fact]
    public void ExtractSections_ParsesHeadings()
    {
        var content = """
            # Title

            Overview content here.

            ## Section One

            Section one content.

            ## Section Two

            Section two content.
            """;

        var sections = _parser.ExtractSections(content);

        Assert.Equal(3, sections.Count);
        Assert.Equal("Title", sections[0].Header);
        Assert.Equal(1, sections[0].Level);
        Assert.Equal("Section One", sections[1].Header);
        Assert.Equal(2, sections[1].Level);
        Assert.Equal("Section Two", sections[2].Header);
        Assert.Equal(2, sections[2].Level);
    }

    [Fact]
    public void ExtractSections_ExtractsContent()
    {
        var content = """
            ## Overview

            This is the overview.

            ## Details

            These are the details.
            """;

        var sections = _parser.ExtractSections(content);

        Assert.Equal(2, sections.Count);
        Assert.Contains("This is the overview.", sections[0].Content);
        Assert.Contains("These are the details.", sections[1].Content);
    }

    [Fact]
    public void ExtractSections_EmptyContent_ReturnsEmpty()
    {
        var sections = _parser.ExtractSections(string.Empty);
        Assert.Empty(sections);
    }

    [Fact]
    public void ExtractSections_NoHeadings_ReturnsEmpty()
    {
        var sections = _parser.ExtractSections("Just some text without headings.");
        Assert.Empty(sections);
    }

    [Fact]
    public void ExtractSection_FindsByHeader()
    {
        var content = """
            ## Overview

            Overview content.

            ## Acceptance Criteria

            - [ ] Test passes
            """;

        var result = _parser.ExtractSection(content, "Acceptance Criteria");

        Assert.NotNull(result);
        Assert.Contains("Test passes", result);
    }

    [Fact]
    public void ExtractSection_CaseInsensitive()
    {
        var content = """
            ## Overview

            Content here.
            """;

        var result = _parser.ExtractSection(content, "overview");
        Assert.NotNull(result);
    }

    [Fact]
    public void ExtractSection_NotFound_ReturnsNull()
    {
        var content = """
            ## Overview

            Content here.
            """;

        var result = _parser.ExtractSection(content, "NonExistent");
        Assert.Null(result);
    }

    [Fact]
    public void ExtractSections_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => _parser.ExtractSections(null!));
    }

    [Fact]
    public void ExtractSection_ThrowsOnNullContent()
    {
        Assert.Throws<ArgumentNullException>(() => _parser.ExtractSection(null!, "header"));
    }

    [Fact]
    public void ExtractSection_ThrowsOnNullHeader()
    {
        Assert.Throws<ArgumentNullException>(() => _parser.ExtractSection("content", null!));
    }

    [Fact]
    public void ImplementsInterface()
    {
        Assert.IsAssignableFrom<ISpecificationParser>(_parser);
    }
}
