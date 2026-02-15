using Lopen.Core.Documents;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Documents;

public sealed class SectionExtractorTests
{
    private readonly SectionExtractor _extractor;

    public SectionExtractorTests()
    {
        var parser = new MarkdigSpecificationParser();
        _extractor = new SectionExtractor(parser, NullLogger<SectionExtractor>.Instance);
    }

    private const string SampleSpec = """
        # Overview

        This module handles authentication.

        # Authentication

        JWT tokens are validated with HMAC-SHA256.

        # Acceptance Criteria

        - [ ] Auth works
        - [ ] Tokens validated
        """;

    [Fact]
    public void ExtractRelevantSections_MatchingHeaders_ReturnsSections()
    {
        var headers = new List<string> { "Authentication" };

        var result = _extractor.ExtractRelevantSections(SampleSpec, headers);

        Assert.Single(result);
        Assert.Equal("Authentication", result[0].Header);
        Assert.Contains("JWT", result[0].Content);
    }

    [Fact]
    public void ExtractRelevantSections_MultipleHeaders_ReturnsMultiple()
    {
        var headers = new List<string> { "Overview", "Authentication" };

        var result = _extractor.ExtractRelevantSections(SampleSpec, headers);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ExtractRelevantSections_CaseInsensitive()
    {
        var headers = new List<string> { "authentication" };

        var result = _extractor.ExtractRelevantSections(SampleSpec, headers);

        Assert.Single(result);
    }

    [Fact]
    public void ExtractRelevantSections_NoMatch_ReturnsEmpty()
    {
        var headers = new List<string> { "Nonexistent" };

        var result = _extractor.ExtractRelevantSections(SampleSpec, headers);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractRelevantSections_EmptyHeaders_ReturnsEmpty()
    {
        var result = _extractor.ExtractRelevantSections(SampleSpec, []);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractRelevantSections_HasTokenEstimates()
    {
        var headers = new List<string> { "Authentication" };

        var result = _extractor.ExtractRelevantSections(SampleSpec, headers);

        Assert.True(result[0].EstimatedTokens > 0);
    }

    [Fact]
    public void ExtractAllSections_ReturnsAllSections()
    {
        var result = _extractor.ExtractAllSections(SampleSpec);

        Assert.Equal(3, result.Count);
        Assert.Equal("Overview", result[0].Header);
        Assert.Equal("Authentication", result[1].Header);
        Assert.Equal("Acceptance Criteria", result[2].Header);
    }

    [Fact]
    public void ExtractAllSections_HasTokenEstimates()
    {
        var result = _extractor.ExtractAllSections(SampleSpec);

        Assert.All(result, s => Assert.True(s.EstimatedTokens > 0));
    }

    [Fact]
    public void ExtractRelevantSections_NullContent_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => _extractor.ExtractRelevantSections(null!, ["header"]));
    }

    [Fact]
    public void ExtractRelevantSections_NullHeaders_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => _extractor.ExtractRelevantSections("content", null!));
    }

    [Fact]
    public void ExtractAllSections_NullContent_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => _extractor.ExtractAllSections(null!));
    }

    [Fact]
    public void Constructor_NullParser_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SectionExtractor(null!, NullLogger<SectionExtractor>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SectionExtractor(new MarkdigSpecificationParser(), null!));
    }

    [Fact]
    public void EstimateTokens_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, SectionExtractor.EstimateTokens(""));
    }

    [Fact]
    public void EstimateTokens_NullString_ReturnsZero()
    {
        Assert.Equal(0, SectionExtractor.EstimateTokens(null!));
    }

    [Fact]
    public void EstimateTokens_FourChars_ReturnsOne()
    {
        Assert.Equal(1, SectionExtractor.EstimateTokens("abcd"));
    }

    [Fact]
    public void EstimateTokens_RoundsUp()
    {
        // 5 chars / 4 = 1.25 → rounds up to 2
        Assert.Equal(2, SectionExtractor.EstimateTokens("abcde"));
    }
}
