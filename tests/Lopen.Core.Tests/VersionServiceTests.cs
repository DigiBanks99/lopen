using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Lopen.Core.Tests;

public class VersionServiceTests
{
    [Fact]
    public void GetVersion_ReturnsSemanticVersion()
    {
        var service = new VersionService();

        var version = service.GetVersion();

        version.Should().MatchRegex(@"^\d+\.\d+\.\d+$");
    }

    [Fact]
    public void GetVersion_WithSpecificAssembly_ReturnsVersionFromAssembly()
    {
        var assembly = typeof(VersionService).Assembly;
        var service = new VersionService(assembly);

        var version = service.GetVersion();

        version.Should().NotBeNullOrEmpty();
        version.Should().MatchRegex(@"^\d+\.\d+\.\d+");
    }

    [Fact]
    public void FormatAsText_ReturnsFormattedString()
    {
        var service = new VersionService();

        var text = service.FormatAsText("lopen");

        text.Should().StartWith("lopen version ");
        text.Should().MatchRegex(@"lopen version \d+\.\d+\.\d+");
    }

    [Fact]
    public void FormatAsJson_ReturnsValidJson()
    {
        var service = new VersionService();

        var json = service.FormatAsJson();

        json.Should().Contain("\"version\"");
        json.Should().MatchRegex(@"\{""version"":""\d+\.\d+\.\d+""\}");
    }

    [Fact]
    public void Constructor_WithNullAssembly_ThrowsArgumentNullException()
    {
        Action act = () => new VersionService(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("assembly");
    }
}
