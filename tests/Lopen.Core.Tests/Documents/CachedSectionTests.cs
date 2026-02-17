using Lopen.Core.Documents;

namespace Lopen.Core.Tests.Documents;

public class CachedSectionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var section = new CachedSection(
            "/docs/requirements/core/SPECIFICATION.md",
            "Acceptance Criteria",
            "- [ ] All tests pass",
            "ABC123",
            timestamp);

        Assert.Equal("/docs/requirements/core/SPECIFICATION.md", section.FilePath);
        Assert.Equal("Acceptance Criteria", section.Header);
        Assert.Equal("- [ ] All tests pass", section.Content);
        Assert.Equal("ABC123", section.ContentHash);
        Assert.Equal(timestamp, section.Timestamp);
    }

    [Fact]
    public void Equality_WorksByValue()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var a = new CachedSection("file", "header", "content", "hash", timestamp);
        var b = new CachedSection("file", "header", "content", "hash", timestamp);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Inequality_DifferentContent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var a = new CachedSection("file", "header", "content1", "hash1", timestamp);
        var b = new CachedSection("file", "header", "content2", "hash2", timestamp);
        Assert.NotEqual(a, b);
    }
}
