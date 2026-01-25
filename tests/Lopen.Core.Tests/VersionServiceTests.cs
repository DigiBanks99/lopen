using System.Reflection;
using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class VersionServiceTests
{
    [Fact]
    public void GetVersion_ReturnsSemanticVersion()
    {
        var service = new VersionService();

        var version = service.GetVersion();

        version.ShouldMatch(@"^\d+\.\d+\.\d+$");
    }

    [Fact]
    public void GetVersion_WithSpecificAssembly_ReturnsVersionFromAssembly()
    {
        var assembly = typeof(VersionService).Assembly;
        var service = new VersionService(assembly);

        var version = service.GetVersion();

        version.ShouldNotBeNullOrEmpty();
        version.ShouldMatch(@"^\d+\.\d+\.\d+");
    }

    [Fact]
    public void FormatAsText_ReturnsFormattedString()
    {
        var service = new VersionService();

        var text = service.FormatAsText("lopen");

        text.ShouldStartWith("lopen version ");
        text.ShouldMatch(@"lopen version \d+\.\d+\.\d+");
    }

    [Fact]
    public void FormatAsJson_ReturnsValidJson()
    {
        var service = new VersionService();

        var json = service.FormatAsJson();

        json.ShouldContain("\"version\"");
        json.ShouldMatch(@"\{""version"":""\d+\.\d+\.\d+""\}");
    }

    [Fact]
    public void Constructor_WithNullAssembly_ThrowsArgumentNullException()
    {
        Action act = () => new VersionService(null!);

        var ex = Should.Throw<ArgumentNullException>(act);
        ex.ParamName.ShouldBe("assembly");
    }
}
