using Lopen.Llm.Tools;
using System.Text.Json;

namespace Lopen.Llm.Tests.Tools;

public sealed class VerificationOperationsTests
{
    [Fact]
    public async Task VerifyTaskCompletion_ReturnsError_WhenTaskIdEmpty()
    {
        var tracker = new FakeVerificationTracker();

        var result = await VerificationOperations.VerifyTaskCompletion(null, tracker, "", null, null);

        var doc = JsonDocument.Parse(result);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Contains("required", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task VerifyTaskCompletion_AutoPasses_WhenOracleAbsent()
    {
        var tracker = new FakeVerificationTracker();

        var result = await VerificationOperations.VerifyTaskCompletion(
            null, tracker, "task-1", "some evidence", "some criteria");

        var doc = JsonDocument.Parse(result);
        Assert.Equal("success", doc.RootElement.GetProperty("status").GetString());
        Assert.True(tracker.IsVerified(VerificationScope.Task, "task-1"));
    }

    [Fact]
    public async Task VerifyTaskCompletion_PassesVerdict_WhenOracleSucceeds()
    {
        var oracle = new FakeOracleVerifier(new OracleVerdict(true, [], VerificationScope.Task));
        var tracker = new FakeVerificationTracker();

        var result = await VerificationOperations.VerifyTaskCompletion(
            oracle, tracker, "task-1", "evidence", "criteria");

        var doc = JsonDocument.Parse(result);
        Assert.Equal("success", doc.RootElement.GetProperty("status").GetString());
        Assert.True(tracker.IsVerified(VerificationScope.Task, "task-1"));
    }

    [Fact]
    public async Task VerifyTaskCompletion_FailsWithGaps_WhenOracleFails()
    {
        var oracle = new FakeOracleVerifier(
            new OracleVerdict(false, ["Missing tests", "No error handling"], VerificationScope.Task));
        var tracker = new FakeVerificationTracker();

        var result = await VerificationOperations.VerifyTaskCompletion(
            oracle, tracker, "task-1", "evidence", "criteria");

        var doc = JsonDocument.Parse(result);
        Assert.Equal("fail", doc.RootElement.GetProperty("status").GetString());
        var message = doc.RootElement.GetProperty("message").GetString()!;
        Assert.Contains("Missing tests", message);
        Assert.Contains("No error handling", message);
        Assert.False(tracker.IsVerified(VerificationScope.Task, "task-1"));
    }

    [Fact]
    public async Task VerifyComponentCompletion_DispatchesOracle()
    {
        var oracle = new FakeOracleVerifier(new OracleVerdict(true, [], VerificationScope.Component));
        var tracker = new FakeVerificationTracker();

        var result = await VerificationOperations.VerifyComponentCompletion(
            oracle, tracker, "comp-1", "evidence", "criteria");

        var doc = JsonDocument.Parse(result);
        Assert.Equal("success", doc.RootElement.GetProperty("status").GetString());
        Assert.True(tracker.IsVerified(VerificationScope.Component, "comp-1"));
    }

    [Fact]
    public async Task VerifyModuleCompletion_DispatchesOracle()
    {
        var oracle = new FakeOracleVerifier(new OracleVerdict(true, [], VerificationScope.Module));
        var tracker = new FakeVerificationTracker();

        var result = await VerificationOperations.VerifyModuleCompletion(
            oracle, tracker, "mod-1", "evidence", "criteria");

        var doc = JsonDocument.Parse(result);
        Assert.Equal("success", doc.RootElement.GetProperty("status").GetString());
        Assert.True(tracker.IsVerified(VerificationScope.Module, "mod-1"));
    }

    #region Fakes

    private sealed class FakeVerificationTracker : IVerificationTracker
    {
        private readonly Dictionary<(VerificationScope, string), bool> _records = new();

        public void RecordVerification(VerificationScope scope, string identifier, bool passed) =>
            _records[(scope, identifier)] = passed;

        public bool IsVerified(VerificationScope scope, string identifier) =>
            _records.TryGetValue((scope, identifier), out var passed) && passed;

        public void ResetForInvocation() => _records.Clear();
    }

    private sealed class FakeOracleVerifier(OracleVerdict verdict) : IOracleVerifier
    {
        public Task<OracleVerdict> VerifyAsync(
            VerificationScope scope, string evidence, string acceptanceCriteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(verdict);
    }

    #endregion
}
