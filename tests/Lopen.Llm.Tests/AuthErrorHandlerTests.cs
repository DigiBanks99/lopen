using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Llm.Tests;

public class AuthErrorHandlerTests
{
    private readonly SpySessionStateSaver _stateSaver = new();
    private readonly AuthErrorHandler _handler;

    public AuthErrorHandlerTests()
    {
        _handler = new AuthErrorHandler(
            _stateSaver,
            NullLogger<AuthErrorHandler>.Instance);
    }

    [Fact]
    public void Constructor_NullStateSaver_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AuthErrorHandler(null!, NullLogger<AuthErrorHandler>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AuthErrorHandler(new SpySessionStateSaver(), null!));
    }

    [Fact]
    public async Task HandleErrorAsync_NullInput_ThrowsArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.HandleErrorAsync(null!));
    }

    [Theory]
    [InlineData("HTTP 401 Unauthorized")]
    [InlineData("HTTP 403 Forbidden")]
    [InlineData("unauthorized access to resource")]
    [InlineData("forbidden: insufficient permissions")]
    [InlineData("authentication failed")]
    [InlineData("auth token expired")]
    public async Task HandleErrorAsync_RecoverableAuthError_ReturnsRetry(string error)
    {
        var input = new ErrorOccurredHookInput
        {
            Error = error,
            Recoverable = true,
        };

        var result = await _handler.HandleErrorAsync(input);

        Assert.NotNull(result);
        Assert.Equal("retry", result!.ErrorHandling);
        Assert.False(_stateSaver.WasCalled);
    }

    [Fact]
    public async Task HandleErrorAsync_RecoverableAuthError_RetryLimitedToOne()
    {
        var input = new ErrorOccurredHookInput
        {
            Error = "401 Unauthorized",
            Recoverable = true,
        };

        // First retry succeeds
        var first = await _handler.HandleErrorAsync(input);
        Assert.Equal("retry", first!.ErrorHandling);

        // Second retry exhausted → abort
        var second = await _handler.HandleErrorAsync(input);
        Assert.Equal("abort", second!.ErrorHandling);
        Assert.True(_stateSaver.WasCalled);
    }

    [Fact]
    public async Task HandleErrorAsync_NonRecoverableAuthError_ReturnsAbort()
    {
        var input = new ErrorOccurredHookInput
        {
            Error = "401 token revoked",
            Recoverable = false,
        };

        var result = await _handler.HandleErrorAsync(input);

        Assert.NotNull(result);
        Assert.Equal("abort", result!.ErrorHandling);
        Assert.True(_stateSaver.WasCalled);
        Assert.Contains("lopen auth login", result.UserNotification);
    }

    [Fact]
    public async Task HandleErrorAsync_NonRecoverableAuthError_SavesSessionState()
    {
        var input = new ErrorOccurredHookInput
        {
            Error = "403 Forbidden - token revoked",
            Recoverable = false,
        };

        await _handler.HandleErrorAsync(input);

        Assert.Equal(1, _stateSaver.SaveCallCount);
    }

    [Theory]
    [InlineData("rate limit exceeded")]
    [InlineData("timeout connecting to server")]
    [InlineData("model not found")]
    [InlineData("")]
    public async Task HandleErrorAsync_NonAuthError_ReturnsNull(string error)
    {
        var input = new ErrorOccurredHookInput
        {
            Error = error,
            Recoverable = true,
        };

        var result = await _handler.HandleErrorAsync(input);

        Assert.Null(result);
        Assert.False(_stateSaver.WasCalled);
    }

    [Fact]
    public async Task HandleErrorAsync_AbortIncludesUserNotification()
    {
        var input = new ErrorOccurredHookInput
        {
            Error = "unauthorized",
            Recoverable = false,
        };

        var result = await _handler.HandleErrorAsync(input);

        Assert.NotNull(result!.UserNotification);
        Assert.Contains("GH_TOKEN", result.UserNotification);
        Assert.Contains("lopen auth login", result.UserNotification);
    }

    [Fact]
    public async Task HandleErrorAsync_SaveFailure_StillReturnsAbort()
    {
        var failingSaver = new FailingSessionStateSaver();
        var handler = new AuthErrorHandler(
            failingSaver,
            NullLogger<AuthErrorHandler>.Instance);

        var input = new ErrorOccurredHookInput
        {
            Error = "401 token expired",
            Recoverable = false,
        };

        var result = await handler.HandleErrorAsync(input);

        Assert.Equal("abort", result!.ErrorHandling);
    }

    [Fact]
    public async Task ResetRetryCount_AllowsNewRetry()
    {
        var input = new ErrorOccurredHookInput
        {
            Error = "401 Unauthorized",
            Recoverable = true,
        };

        var first = await _handler.HandleErrorAsync(input);
        Assert.Equal("retry", first!.ErrorHandling);

        _handler.ResetRetryCount();

        var afterReset = await _handler.HandleErrorAsync(input);
        Assert.Equal("retry", afterReset!.ErrorHandling);
    }

    // -- Test helpers --

    private sealed class SpySessionStateSaver : ISessionStateSaver
    {
        public bool WasCalled => SaveCallCount > 0;
        public int SaveCallCount { get; private set; }

        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingSessionStateSaver : ISessionStateSaver
    {
        public Task SaveAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Save failed");
    }
}
