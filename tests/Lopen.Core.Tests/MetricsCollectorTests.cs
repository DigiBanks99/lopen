using Shouldly;

namespace Lopen.Core.Tests;

/// <summary>
/// Unit tests for MetricsCollector.
/// </summary>
public class MetricsCollectorTests
{
    [Fact]
    public void StartRequest_CreatesMetrics()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        var metrics = collector.StartRequest();

        // Assert
        metrics.ShouldNotBeNull();
        metrics.RequestTime.ShouldNotBe(default);
    }

    [Fact]
    public void StartRequest_WithRequestId_TracksById()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        collector.StartRequest("req-123");

        // Assert
        var metrics = collector.GetMetrics("req-123");
        metrics.ShouldNotBeNull();
    }

    [Fact]
    public void RecordFirstToken_UpdatesMetrics()
    {
        // Arrange
        var collector = new MetricsCollector();
        collector.StartRequest();

        // Act
        collector.RecordFirstToken();

        // Assert
        var metrics = collector.GetLatestMetrics();
        metrics.ShouldNotBeNull();
        metrics.FirstTokenTime.ShouldNotBeNull();
    }

    [Fact]
    public void RecordFirstToken_OnlyRecordsOnce()
    {
        // Arrange
        var collector = new MetricsCollector();
        collector.StartRequest();

        // Act
        collector.RecordFirstToken();
        var firstTime = collector.GetLatestMetrics()?.FirstTokenTime;
        Thread.Sleep(10);
        collector.RecordFirstToken();

        // Assert
        var metrics = collector.GetLatestMetrics();
        metrics?.FirstTokenTime.ShouldBe(firstTime);
    }

    [Fact]
    public void RecordFirstToken_WithRequestId_UpdatesCorrectRequest()
    {
        // Arrange
        var collector = new MetricsCollector();
        collector.StartRequest("req-1");
        collector.StartRequest("req-2");

        // Act
        collector.RecordFirstToken("req-1");

        // Assert
        var metrics1 = collector.GetMetrics("req-1");
        var metrics2 = collector.GetMetrics("req-2");
        metrics1?.FirstTokenTime.ShouldNotBeNull();
        metrics2?.FirstTokenTime.ShouldBeNull();
    }

    [Fact]
    public void RecordCompletion_UpdatesMetrics()
    {
        // Arrange
        var collector = new MetricsCollector();
        collector.StartRequest();
        collector.RecordFirstToken();

        // Act
        collector.RecordCompletion(tokenCount: 100, bytesReceived: 1000);

        // Assert
        var metrics = collector.GetLatestMetrics();
        metrics.ShouldNotBeNull();
        metrics.CompletionTime.ShouldNotBeNull();
        metrics.TokenCount.ShouldBe(100);
        metrics.BytesReceived.ShouldBe(1000);
    }

    [Fact]
    public void RecordCompletion_AddsToHistory()
    {
        // Arrange
        var collector = new MetricsCollector();
        collector.StartRequest();
        collector.RecordFirstToken();

        // Act
        collector.RecordCompletion(50, 500);

        // Assert
        var history = collector.GetAllMetrics();
        history.Count.ShouldBe(1);
        history[0].TokenCount.ShouldBe(50);
    }

    [Fact]
    public void GetLatestMetrics_ReturnsDefaultRequest()
    {
        // Arrange
        var collector = new MetricsCollector();
        collector.StartRequest();

        // Act
        var metrics = collector.GetLatestMetrics();

        // Assert
        metrics.ShouldNotBeNull();
    }

    [Fact]
    public void GetLatestMetrics_NullWhenNoRequest()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        var metrics = collector.GetLatestMetrics();

        // Assert
        metrics.ShouldBeNull();
    }

    [Fact]
    public void GetAllMetrics_ReturnsCompletedRequests()
    {
        // Arrange
        var collector = new MetricsCollector();

        collector.StartRequest("req-1");
        collector.RecordCompletion(10, 100, "req-1");

        collector.StartRequest("req-2");
        collector.RecordCompletion(20, 200, "req-2");

        // Act
        var history = collector.GetAllMetrics();

        // Assert
        history.Count.ShouldBe(2);
    }

    [Fact]
    public void Clear_RemovesAllMetrics()
    {
        // Arrange
        var collector = new MetricsCollector();
        collector.StartRequest();
        collector.RecordCompletion(10, 100);

        // Act
        collector.Clear();

        // Assert
        collector.GetLatestMetrics().ShouldBeNull();
        collector.GetAllMetrics().Count.ShouldBe(0);
    }

    [Fact]
    public void MultipleRequests_TrackedIndependently()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        collector.StartRequest("fast");
        collector.StartRequest("slow");

        collector.RecordFirstToken("fast");
        Thread.Sleep(10);
        collector.RecordFirstToken("slow");

        collector.RecordCompletion(10, 100, "fast");
        collector.RecordCompletion(20, 200, "slow");

        // Assert
        var fast = collector.GetMetrics("fast");
        var slow = collector.GetMetrics("slow");

        fast.ShouldNotBeNull();
        slow.ShouldNotBeNull();
        fast.TokenCount.ShouldBe(10);
        slow.TokenCount.ShouldBe(20);
    }

    [Fact]
    public void RecordFirstToken_NoOpWhenNoRequest()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act & Assert - should not throw
        collector.RecordFirstToken();
        collector.GetLatestMetrics().ShouldBeNull();
    }

    [Fact]
    public void RecordCompletion_NoOpWhenNoRequest()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act & Assert - should not throw
        collector.RecordCompletion(10, 100);
        collector.GetAllMetrics().Count.ShouldBe(0);
    }
}
