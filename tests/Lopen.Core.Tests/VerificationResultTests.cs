using Shouldly;

namespace Lopen.Core.Tests;

public class VerificationResultTests
{
    [Fact]
    public void Passed_ReturnsCompleteResult()
    {
        var result = VerificationResult.Passed();

        result.Complete.ShouldBeTrue();
        result.TestsPass.ShouldBeTrue();
        result.DocumentationExists.ShouldBeTrue();
        result.BuildSucceeds.ShouldBeTrue();
        result.RequirementValid.ShouldBeTrue();
        result.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Failed_ReturnsIncompleteResultWithIssue()
    {
        var result = VerificationResult.Failed("Test failure");

        result.Complete.ShouldBeFalse();
        result.Issues.ShouldContain("Test failure");
    }

    [Fact]
    public void DefaultResult_HasEmptyIssues()
    {
        var result = new VerificationResult();

        result.Issues.ShouldNotBeNull();
        result.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Result_CanSetAllProperties()
    {
        var issues = new List<string> { "Issue 1", "Issue 2" };
        var result = new VerificationResult
        {
            Complete = true,
            TestsPass = true,
            DocumentationExists = false,
            BuildSucceeds = true,
            RequirementValid = true,
            Issues = issues
        };

        result.Complete.ShouldBeTrue();
        result.TestsPass.ShouldBeTrue();
        result.DocumentationExists.ShouldBeFalse();
        result.BuildSucceeds.ShouldBeTrue();
        result.RequirementValid.ShouldBeTrue();
        result.Issues.Count.ShouldBe(2);
    }

    [Fact]
    public void Result_IsImmutable()
    {
        var original = VerificationResult.Passed();
        var modified = original with { Complete = false };

        original.Complete.ShouldBeTrue();
        modified.Complete.ShouldBeFalse();
    }
}
