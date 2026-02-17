using Lopen.Core.Documents;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Documents;

public class DriftDetectorTests
{
    private readonly XxHashContentHasher _hasher = new();
    private readonly MarkdigSpecificationParser _parser = new();
    private readonly DriftDetector _detector;

    public DriftDetectorTests()
    {
        _detector = new DriftDetector(_parser, _hasher, NullLogger<DriftDetector>.Instance);
    }

    [Fact]
    public void DetectDrift_NoDrift_ReturnsEmpty()
    {
        var content = "# Overview\n\nSome content\n\n# Details\n\nMore content";
        var sections = _parser.ExtractSections(content);
        var cached = sections.Select(s =>
            new CachedSection("spec.md", s.Header, s.Content, _hasher.ComputeHash(s.Content), DateTimeOffset.UtcNow))
            .ToList();

        var results = _detector.DetectDrift("spec.md", content, cached);

        Assert.Empty(results);
    }

    [Fact]
    public void DetectDrift_ContentChanged_ReturnsModified()
    {
        var original = "# Overview\n\nOriginal content";
        var modified = "# Overview\n\nModified content";
        var sections = _parser.ExtractSections(original);
        var cached = sections.Select(s =>
            new CachedSection("spec.md", s.Header, s.Content, _hasher.ComputeHash(s.Content), DateTimeOffset.UtcNow))
            .ToList();

        var results = _detector.DetectDrift("spec.md", modified, cached);

        Assert.Single(results);
        Assert.Equal("Overview", results[0].Header);
        Assert.False(results[0].IsNew);
        Assert.False(results[0].IsRemoved);
    }

    [Fact]
    public void DetectDrift_NewSection_ReturnsNew()
    {
        var original = "# Overview\n\nContent";
        var updated = "# Overview\n\nContent\n\n# New Section\n\nNew stuff";
        var sections = _parser.ExtractSections(original);
        var cached = sections.Select(s =>
            new CachedSection("spec.md", s.Header, s.Content, _hasher.ComputeHash(s.Content), DateTimeOffset.UtcNow))
            .ToList();

        var results = _detector.DetectDrift("spec.md", updated, cached);

        Assert.Single(results);
        Assert.Equal("New Section", results[0].Header);
        Assert.True(results[0].IsNew);
    }

    [Fact]
    public void DetectDrift_RemovedSection_ReturnsRemoved()
    {
        var original = "# Overview\n\nContent\n\n# Details\n\nMore";
        var updated = "# Overview\n\nContent";
        var sections = _parser.ExtractSections(original);
        var cached = sections.Select(s =>
            new CachedSection("spec.md", s.Header, s.Content, _hasher.ComputeHash(s.Content), DateTimeOffset.UtcNow))
            .ToList();

        var results = _detector.DetectDrift("spec.md", updated, cached);

        Assert.Single(results);
        Assert.Equal("Details", results[0].Header);
        Assert.True(results[0].IsRemoved);
    }

    [Fact]
    public void DetectDrift_EmptyCache_AllSectionsAreNew()
    {
        var content = "# Overview\n\nContent\n\n# Details\n\nMore";

        var results = _detector.DetectDrift("spec.md", content, []);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.IsNew));
    }

    [Fact]
    public void DetectDrift_DifferentFilePath_IgnoresCache()
    {
        var content = "# Overview\n\nContent";
        var cached = new List<CachedSection>
        {
            new("other.md", "Overview", "Content", _hasher.ComputeHash("Content"), DateTimeOffset.UtcNow),
        };

        var results = _detector.DetectDrift("spec.md", content, cached);

        Assert.Single(results);
        Assert.True(results[0].IsNew);
    }

    [Fact]
    public void DetectDrift_NullPath_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => _detector.DetectDrift(null!, "content", []));
    }

    [Fact]
    public void DetectDrift_NullContent_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => _detector.DetectDrift("spec.md", null!, []));
    }

    [Fact]
    public void DetectDrift_NullCache_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => _detector.DetectDrift("spec.md", "content", null!));
    }

    [Fact]
    public void Constructor_NullParser_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new DriftDetector(null!, _hasher, NullLogger<DriftDetector>.Instance));
    }

    [Fact]
    public void Constructor_NullHasher_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new DriftDetector(_parser, null!, NullLogger<DriftDetector>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new DriftDetector(_parser, _hasher, null!));
    }
}
