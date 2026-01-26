using Shouldly;

namespace Lopen.Core.Tests;

/// <summary>
/// Unit tests for ResponseMetrics.
/// </summary>
public class ResponseMetricsTests
{
    [Fact]
    public void Started_SetsRequestTime()
    {
        // Act
        var metrics = ResponseMetrics.Started();

        // Assert
        metrics.RequestTime.ShouldNotBe(default);
        metrics.FirstTokenTime.ShouldBeNull();
        metrics.CompletionTime.ShouldBeNull();
        metrics.TokenCount.ShouldBe(0);
        metrics.BytesReceived.ShouldBe(0);
    }

    [Fact]
    public void WithFirstToken_SetsFirstTokenTime()
    {
        // Arrange
        var metrics = ResponseMetrics.Started();

        // Act
        var updated = metrics.WithFirstToken();

        // Assert
        updated.FirstTokenTime.ShouldNotBeNull();
        updated.FirstTokenTime.Value.ShouldBeGreaterThanOrEqualTo(updated.RequestTime);
        updated.RequestTime.ShouldBe(metrics.RequestTime);
    }

    [Fact]
    public void WithCompletion_SetsCompletionData()
    {
        // Arrange
        var metrics = ResponseMetrics.Started().WithFirstToken();

        // Act
        var completed = metrics.WithCompletion(tokenCount: 50, bytesReceived: 500);

        // Assert
        completed.CompletionTime.ShouldNotBeNull();
        completed.TokenCount.ShouldBe(50);
        completed.BytesReceived.ShouldBe(500);
    }

    [Fact]
    public void TimeToFirstToken_CalculatesCorrectly()
    {
        // Arrange
        var requestTime = DateTimeOffset.UtcNow;
        var firstTokenTime = requestTime.AddMilliseconds(150);

        var metrics = new ResponseMetrics
        {
            RequestTime = requestTime,
            FirstTokenTime = firstTokenTime
        };

        // Assert
        metrics.TimeToFirstToken.ShouldNotBeNull();
        metrics.TimeToFirstToken.Value.TotalMilliseconds.ShouldBe(150, tolerance: 0.1);
    }

    [Fact]
    public void TimeToFirstToken_NullWhenNoFirstToken()
    {
        // Arrange
        var metrics = ResponseMetrics.Started();

        // Assert
        metrics.TimeToFirstToken.ShouldBeNull();
    }

    [Fact]
    public void TotalTime_CalculatesCorrectly()
    {
        // Arrange
        var requestTime = DateTimeOffset.UtcNow;
        var completionTime = requestTime.AddMilliseconds(2500);

        var metrics = new ResponseMetrics
        {
            RequestTime = requestTime,
            CompletionTime = completionTime
        };

        // Assert
        metrics.TotalTime.ShouldNotBeNull();
        metrics.TotalTime.Value.TotalMilliseconds.ShouldBe(2500, tolerance: 0.1);
    }

    [Fact]
    public void TotalTime_NullWhenNoCompletion()
    {
        // Arrange
        var metrics = ResponseMetrics.Started();

        // Assert
        metrics.TotalTime.ShouldBeNull();
    }

    [Fact]
    public void TokensPerSecond_CalculatesCorrectly()
    {
        // Arrange
        var requestTime = DateTimeOffset.UtcNow;
        var firstTokenTime = requestTime.AddMilliseconds(100);
        var completionTime = firstTokenTime.AddSeconds(2); // 2 seconds of streaming

        var metrics = new ResponseMetrics
        {
            RequestTime = requestTime,
            FirstTokenTime = firstTokenTime,
            CompletionTime = completionTime,
            TokenCount = 21 // First token + 20 more = 10/s
        };

        // Assert
        metrics.TokensPerSecond.ShouldNotBeNull();
        metrics.TokensPerSecond.Value.ShouldBe(10.0, tolerance: 0.1);
    }

    [Fact]
    public void TokensPerSecond_NullWhenSingleToken()
    {
        // Arrange
        var metrics = new ResponseMetrics
        {
            RequestTime = DateTimeOffset.UtcNow,
            FirstTokenTime = DateTimeOffset.UtcNow.AddMilliseconds(100),
            CompletionTime = DateTimeOffset.UtcNow.AddMilliseconds(200),
            TokenCount = 1
        };

        // Assert
        metrics.TokensPerSecond.ShouldBeNull();
    }

    [Fact]
    public void TokensPerSecond_NullWhenNoFirstToken()
    {
        // Arrange
        var metrics = new ResponseMetrics
        {
            RequestTime = DateTimeOffset.UtcNow,
            CompletionTime = DateTimeOffset.UtcNow.AddSeconds(1),
            TokenCount = 10
        };

        // Assert
        metrics.TokensPerSecond.ShouldBeNull();
    }

    [Fact]
    public void MeetsFirstTokenTarget_TrueWhenUnder2Seconds()
    {
        // Arrange
        var requestTime = DateTimeOffset.UtcNow;
        var firstTokenTime = requestTime.AddMilliseconds(1500); // 1.5s

        var metrics = new ResponseMetrics
        {
            RequestTime = requestTime,
            FirstTokenTime = firstTokenTime
        };

        // Assert
        metrics.MeetsFirstTokenTarget.ShouldBeTrue();
    }

    [Fact]
    public void MeetsFirstTokenTarget_FalseWhenOver2Seconds()
    {
        // Arrange
        var requestTime = DateTimeOffset.UtcNow;
        var firstTokenTime = requestTime.AddMilliseconds(2500); // 2.5s

        var metrics = new ResponseMetrics
        {
            RequestTime = requestTime,
            FirstTokenTime = firstTokenTime
        };

        // Assert
        metrics.MeetsFirstTokenTarget.ShouldBeFalse();
    }

    [Fact]
    public void MeetsFirstTokenTarget_FalseWhenNoFirstToken()
    {
        // Arrange
        var metrics = ResponseMetrics.Started();

        // Assert
        metrics.MeetsFirstTokenTarget.ShouldBeFalse();
    }

    [Fact]
    public void MeetsFirstTokenTarget_TrueAtExactly1999Ms()
    {
        // Arrange
        var requestTime = DateTimeOffset.UtcNow;
        var firstTokenTime = requestTime.AddMilliseconds(1999);

        var metrics = new ResponseMetrics
        {
            RequestTime = requestTime,
            FirstTokenTime = firstTokenTime
        };

        // Assert
        metrics.MeetsFirstTokenTarget.ShouldBeTrue();
    }
}
