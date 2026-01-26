using Shouldly;

namespace Lopen.Core.Tests;

public class MockVerificationServiceTests
{
    [Fact]
    public async Task VerifyJobCompletionAsync_IncrementsCounter()
    {
        var mock = new MockVerificationService();

        await mock.VerifyJobCompletionAsync("JTBD-001", "REQ-001");

        mock.JobVerificationCount.ShouldBe(1);
    }

    [Fact]
    public async Task VerifyJobCompletionAsync_TracksLastJobId()
    {
        var mock = new MockVerificationService();

        await mock.VerifyJobCompletionAsync("JTBD-025", "REQ-036");

        mock.LastJobId.ShouldBe("JTBD-025");
        mock.LastRequirementCode.ShouldBe("REQ-036");
    }

    [Fact]
    public async Task VerifyTestsAsync_IncrementsCounter()
    {
        var mock = new MockVerificationService();

        await mock.VerifyTestsAsync("REQ-001");

        mock.TestVerificationCount.ShouldBe(1);
    }

    [Fact]
    public async Task VerifyDocumentationAsync_IncrementsCounter()
    {
        var mock = new MockVerificationService();

        await mock.VerifyDocumentationAsync("REQ-001");

        mock.DocVerificationCount.ShouldBe(1);
    }

    [Fact]
    public async Task VerifyBuildAsync_IncrementsCounter()
    {
        var mock = new MockVerificationService();

        await mock.VerifyBuildAsync();

        mock.BuildVerificationCount.ShouldBe(1);
    }

    [Fact]
    public async Task VerifyRequirementCodeAsync_IncrementsCounter()
    {
        var mock = new MockVerificationService();

        await mock.VerifyRequirementCodeAsync("REQ-001");

        mock.RequirementVerificationCount.ShouldBe(1);
    }

    [Fact]
    public async Task SetResult_ChangesReturnedResult()
    {
        var mock = new MockVerificationService();
        var customResult = VerificationResult.Failed("Custom failure");

        mock.SetResult(customResult);
        var result = await mock.VerifyJobCompletionAsync("JTBD-001", "REQ-001");

        result.Complete.ShouldBeFalse();
        result.Issues.ShouldContain("Custom failure");
    }

    [Fact]
    public async Task DefaultResult_IsPassed()
    {
        var mock = new MockVerificationService();

        var result = await mock.VerifyJobCompletionAsync("JTBD-001", "REQ-001");

        result.Complete.ShouldBeTrue();
    }

    [Fact]
    public async Task Reset_ClearsAllCounters()
    {
        var mock = new MockVerificationService();
        await mock.VerifyJobCompletionAsync("JTBD-001", "REQ-001");
        await mock.VerifyTestsAsync("REQ-001");
        mock.SetResult(VerificationResult.Failed("Fail"));

        mock.Reset();

        mock.JobVerificationCount.ShouldBe(0);
        mock.TestVerificationCount.ShouldBe(0);
        mock.LastJobId.ShouldBeNull();
        mock.LastRequirementCode.ShouldBeNull();
    }
}
