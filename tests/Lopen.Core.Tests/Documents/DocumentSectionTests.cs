using Lopen.Core.Documents;

namespace Lopen.Core.Tests.Documents;

public class DocumentSectionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var section = new DocumentSection("Overview", 2, "This is the overview content.");

        Assert.Equal("Overview", section.Header);
        Assert.Equal(2, section.Level);
        Assert.Equal("This is the overview content.", section.Content);
    }

    [Fact]
    public void Equality_WorksByValue()
    {
        var a = new DocumentSection("Header", 1, "Content");
        var b = new DocumentSection("Header", 1, "Content");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Inequality_DifferentLevel()
    {
        var a = new DocumentSection("Header", 1, "Content");
        var b = new DocumentSection("Header", 2, "Content");
        Assert.NotEqual(a, b);
    }
}
