using Lopen.Core.Testing;
using Lopen.Core.Testing.TestSuites;
using Shouldly;

namespace Lopen.Core.Tests;

public class InteractiveTestCaseTests
{
    private static TestResult CreatePassResult() => new()
    {
        TestId = "T-TEST-01",
        Suite = "test",
        Description = "Test description",
        Status = TestStatus.Pass,
        Duration = TimeSpan.FromMilliseconds(10)
    };

    [Fact]
    public async Task ExecuteAsync_WithoutInteractivePrompt_ReturnsSkipped()
    {
        // Arrange
        var testCase = new InteractiveTestCase(
            testId: "T-TEST-01",
            description: "Test description",
            suite: "test",
            executeFunc: (ctx, prompt, ct) => Task.FromResult(CreatePassResult()));
        
        var context = new TestContext { InteractivePrompt = null };

        // Act
        var result = await testCase.ExecuteAsync(context);

        // Assert
        result.Status.ShouldBe(TestStatus.Skipped);
        result.Error!.ShouldContain("interactive");
    }

    [Fact]
    public async Task ExecuteAsync_WithInteractivePrompt_ExecutesFunction()
    {
        // Arrange
        var mockPrompt = new MockInteractivePrompt();
        var executedFlag = false;

        var testCase = new InteractiveTestCase(
            testId: "T-TEST-01",
            description: "Test description",
            suite: "test",
            executeFunc: (ctx, prompt, ct) =>
            {
                executedFlag = true;
                return Task.FromResult(CreatePassResult());
            });

        var context = new TestContext { InteractivePrompt = mockPrompt };

        // Act
        var result = await testCase.ExecuteAsync(context);

        // Assert
        executedFlag.ShouldBeTrue();
        result.Status.ShouldBe(TestStatus.Pass);
    }

    [Fact]
    public async Task ExecuteAsync_SetsDurationAndTiming()
    {
        // Arrange
        var mockPrompt = new MockInteractivePrompt();
        var testCase = new InteractiveTestCase(
            testId: "T-TEST-01",
            description: "Test description",
            suite: "test",
            executeFunc: async (ctx, prompt, ct) =>
            {
                await Task.Delay(10);
                return CreatePassResult();
            });

        var context = new TestContext { InteractivePrompt = mockPrompt };

        // Act
        var result = await testCase.ExecuteAsync(context);

        // Assert
        result.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
        result.StartTime.ShouldBeLessThan(result.EndTime);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesException()
    {
        // Arrange
        var mockPrompt = new MockInteractivePrompt();
        var testCase = new InteractiveTestCase(
            testId: "T-TEST-01",
            description: "Test description",
            suite: "test",
            executeFunc: (ctx, prompt, ct) => throw new InvalidOperationException("Test error"));

        var context = new TestContext { InteractivePrompt = mockPrompt };

        // Act
        var result = await testCase.ExecuteAsync(context);

        // Assert
        result.Status.ShouldBe(TestStatus.Error);
        result.Error!.ShouldContain("Test error");
    }

    [Fact]
    public async Task ExecuteAsync_HandlesCancellation()
    {
        // Arrange
        var mockPrompt = new MockInteractivePrompt();
        using var cts = new CancellationTokenSource();
        
        var testCase = new InteractiveTestCase(
            testId: "T-TEST-01",
            description: "Test description",
            suite: "test",
            executeFunc: (ctx, prompt, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(CreatePassResult());
            });

        var context = new TestContext { InteractivePrompt = mockPrompt };
        await cts.CancelAsync();

        // Act
        var result = await testCase.ExecuteAsync(context, cts.Token);

        // Assert
        result.Status.ShouldBe(TestStatus.Skipped);
        result.Error!.ShouldContain("cancelled");
    }

    [Fact]
    public void RequiresInteraction_IsTrue()
    {
        // Arrange
        var testCase = new InteractiveTestCase(
            testId: "T-TEST-01",
            description: "Test description",
            suite: "test",
            executeFunc: (ctx, prompt, ct) => Task.FromResult(CreatePassResult()));

        // Assert
        testCase.RequiresInteraction.ShouldBeTrue();
    }
}

public class MockInteractivePromptTests
{
    [Fact]
    public void DisplayMessage_RecordsMessages()
    {
        // Arrange
        var prompt = new MockInteractivePrompt();

        // Act
        prompt.DisplayMessage("Message 1");
        prompt.DisplayMessage("Message 2");

        // Assert
        prompt.Messages.Count.ShouldBe(2);
        prompt.Messages[0].ShouldBe("Message 1");
        prompt.Messages[1].ShouldBe("Message 2");
    }

    [Fact]
    public void DisplayStep_RecordsSteps()
    {
        // Arrange
        var prompt = new MockInteractivePrompt();

        // Act
        prompt.DisplayStep(1, 3, "First step");
        prompt.DisplayStep(2, 3, "Second step");

        // Assert
        prompt.Steps.Count.ShouldBe(2);
        prompt.Steps[0].ShouldBe((1, 3, "First step"));
        prompt.Steps[1].ShouldBe((2, 3, "Second step"));
    }

    [Fact]
    public void Confirm_ReturnsQueuedResponse()
    {
        // Arrange
        var prompt = new MockInteractivePrompt();
        prompt.QueueConfirmResponse(false);
        prompt.QueueConfirmResponse(true);

        // Act & Assert
        prompt.Confirm("Q1").ShouldBeFalse();
        prompt.Confirm("Q2").ShouldBeTrue();
        prompt.Confirm("Q3").ShouldBeTrue(); // Default when queue empty
    }

    [Fact]
    public void ConfirmSuccess_ReturnsQueuedResponse()
    {
        // Arrange
        var prompt = new MockInteractivePrompt();
        prompt.QueueSuccessResponse(false);

        // Act & Assert
        prompt.ConfirmSuccess("Worked?").ShouldBeFalse();
        prompt.ConfirmSuccess("Worked?").ShouldBeTrue(); // Default
    }

    [Fact]
    public void WaitForContinue_IncrementsCount()
    {
        // Arrange
        var prompt = new MockInteractivePrompt();

        // Act
        prompt.WaitForContinue();
        prompt.WaitForContinue("Custom message");

        // Assert
        prompt.WaitCount.ShouldBe(2);
    }
}

public class AuthTestSuiteInteractiveTests
{
    [Fact]
    public void GetTests_ContainsTwoTests()
    {
        // Act
        var tests = AuthTestSuite.GetTests().ToList();

        // Assert
        tests.Count.ShouldBe(2);
    }

    [Fact]
    public void GetTests_ContainsAuthStatusTest()
    {
        // Act
        var tests = AuthTestSuite.GetTests().ToList();

        // Assert
        var authStatus = tests.FirstOrDefault(t => t.TestId == "T-AUTH-01");
        authStatus.ShouldNotBeNull();
        authStatus.Description.ShouldBe("Check auth status");
        authStatus.Suite.ShouldBe("auth");
    }

    [Fact]
    public void GetTests_ContainsInteractiveDeviceFlowTest()
    {
        // Act
        var tests = AuthTestSuite.GetTests().ToList();

        // Assert
        var deviceFlow = tests.FirstOrDefault(t => t.TestId == "T-AUTH-02");
        deviceFlow.ShouldNotBeNull();
        deviceFlow.Description.ShouldBe("Interactive device auth flow");
        deviceFlow.Suite.ShouldBe("auth");
        deviceFlow.ShouldBeOfType<InteractiveTestCase>();
    }

    [Fact]
    public async Task InteractiveDeviceFlow_WithoutPrompt_ReturnsSkipped()
    {
        // Arrange
        var tests = AuthTestSuite.GetTests().ToList();
        var deviceFlow = tests.First(t => t.TestId == "T-AUTH-02");
        var context = new TestContext { InteractivePrompt = null };

        // Act
        var result = await deviceFlow.ExecuteAsync(context);

        // Assert
        result.Status.ShouldBe(TestStatus.Skipped);
    }

    [Fact]
    public async Task InteractiveDeviceFlow_WithAllConfirmations_Passes()
    {
        // Arrange
        var tests = AuthTestSuite.GetTests().ToList();
        var deviceFlow = tests.First(t => t.TestId == "T-AUTH-02");
        
        var mockPrompt = new MockInteractivePrompt();
        // Queue "yes" responses for all confirmations
        mockPrompt.QueueConfirmResponse(true); // logout
        mockPrompt.QueueConfirmResponse(true); // device code shown
        mockPrompt.QueueConfirmResponse(true); // browser auth
        mockPrompt.QueueConfirmResponse(true); // MFA
        mockPrompt.QueueSuccessResponse(true); // final verification
        
        var context = new TestContext { InteractivePrompt = mockPrompt };

        // Act
        var result = await deviceFlow.ExecuteAsync(context);

        // Assert
        result.Status.ShouldBe(TestStatus.Pass);
        result.TestId.ShouldBe("T-AUTH-02");
    }

    [Fact]
    public async Task InteractiveDeviceFlow_UserSkipsStep_ReturnsSkipped()
    {
        // Arrange
        var tests = AuthTestSuite.GetTests().ToList();
        var deviceFlow = tests.First(t => t.TestId == "T-AUTH-02");
        
        var mockPrompt = new MockInteractivePrompt();
        mockPrompt.QueueConfirmResponse(false); // User skips logout step
        
        var context = new TestContext { InteractivePrompt = mockPrompt };

        // Act
        var result = await deviceFlow.ExecuteAsync(context);

        // Assert
        result.Status.ShouldBe(TestStatus.Skipped);
        result.Error!.ShouldContain("skipped");
    }

    [Fact]
    public async Task InteractiveDeviceFlow_DeviceCodeNotShown_Fails()
    {
        // Arrange
        var tests = AuthTestSuite.GetTests().ToList();
        var deviceFlow = tests.First(t => t.TestId == "T-AUTH-02");
        
        var mockPrompt = new MockInteractivePrompt();
        mockPrompt.QueueConfirmResponse(true);  // logout ok
        mockPrompt.QueueConfirmResponse(false); // device code NOT shown
        
        var context = new TestContext { InteractivePrompt = mockPrompt };

        // Act
        var result = await deviceFlow.ExecuteAsync(context);

        // Assert
        result.Status.ShouldBe(TestStatus.Fail);
        result.Error!.ShouldContain("Device code");
    }

    [Fact]
    public async Task InteractiveDeviceFlow_FinalVerificationFails_ReturnsFail()
    {
        // Arrange
        var tests = AuthTestSuite.GetTests().ToList();
        var deviceFlow = tests.First(t => t.TestId == "T-AUTH-02");
        
        var mockPrompt = new MockInteractivePrompt();
        mockPrompt.QueueConfirmResponse(true); // logout
        mockPrompt.QueueConfirmResponse(true); // device code
        mockPrompt.QueueConfirmResponse(true); // browser
        mockPrompt.QueueConfirmResponse(true); // MFA
        mockPrompt.QueueSuccessResponse(false); // verification failed
        
        var context = new TestContext { InteractivePrompt = mockPrompt };

        // Act
        var result = await deviceFlow.ExecuteAsync(context);

        // Assert
        result.Status.ShouldBe(TestStatus.Fail);
        result.Error!.ShouldContain("failed");
    }

    [Fact]
    public async Task InteractiveDeviceFlow_ShowsCorrectSteps()
    {
        // Arrange
        var tests = AuthTestSuite.GetTests().ToList();
        var deviceFlow = tests.First(t => t.TestId == "T-AUTH-02");
        
        var mockPrompt = new MockInteractivePrompt();
        mockPrompt.QueueConfirmResponse(true);
        mockPrompt.QueueConfirmResponse(true);
        mockPrompt.QueueConfirmResponse(true);
        mockPrompt.QueueConfirmResponse(true);
        mockPrompt.QueueSuccessResponse(true);
        
        var context = new TestContext { InteractivePrompt = mockPrompt };

        // Act
        await deviceFlow.ExecuteAsync(context);

        // Assert
        mockPrompt.Steps.Count.ShouldBe(5);
        mockPrompt.Steps[0].Step.ShouldBe(1);
        mockPrompt.Steps[4].Step.ShouldBe(5);
        mockPrompt.Steps[4].Total.ShouldBe(5);
    }
}
