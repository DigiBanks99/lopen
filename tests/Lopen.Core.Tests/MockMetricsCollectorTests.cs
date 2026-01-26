using Shouldly;

namespace Lopen.Core.Tests;

/// <summary>
/// Unit tests for MockMetricsCollector.
/// </summary>
public class MockMetricsCollectorTests
{
    [Fact]
    public void StartRequest_IncrementsCounter()
    {
        // Arrange
        var mock = new MockMetricsCollector();

        // Act
        mock.StartRequest();
        mock.StartRequest();

        // Assert
        mock.StartRequestCount.ShouldBe(2);
    }

    [Fact]
    public void RecordFirstToken_IncrementsCounter()
    {
        // Arrange
        var mock = new MockMetricsCollector();
        mock.StartRequest();

        // Act
        mock.RecordFirstToken();
        mock.RecordFirstToken();

        // Assert
        mock.FirstTokenCount.ShouldBe(2);
    }

    [Fact]
    public void RecordCompletion_IncrementsCounter()
    {
        // Arrange
        var mock = new MockMetricsCollector();
        mock.StartRequest();

        // Act
        mock.RecordCompletion(10, 100);

        // Assert
        mock.CompletionCount.ShouldBe(1);
    }

    [Fact]
    public void FixedTimeToFirstToken_UsedInMetrics()
    {
        // Arrange
        var mock = new MockMetricsCollector
        {
            FixedTimeToFirstToken = TimeSpan.FromMilliseconds(1500)
        };

        // Act
        mock.StartRequest();
        mock.RecordFirstToken();

        // Assert
        var metrics = mock.GetLatestMetrics();
        metrics.ShouldNotBeNull();
        metrics.TimeToFirstToken.ShouldNotBeNull();
        metrics.TimeToFirstToken.Value.TotalMilliseconds.ShouldBe(1500, tolerance: 0.1);
    }

    [Fact]
    public void FixedTotalTime_UsedInMetrics()
    {
        // Arrange
        var mock = new MockMetricsCollector
        {
            FixedTotalTime = TimeSpan.FromMilliseconds(3000)
        };

        // Act
        mock.StartRequest();
        mock.RecordCompletion(50, 500);

        // Assert
        var metrics = mock.GetLatestMetrics();
        metrics.ShouldNotBeNull();
        metrics.TotalTime.ShouldNotBeNull();
        metrics.TotalTime.Value.TotalMilliseconds.ShouldBe(3000, tolerance: 0.1);
    }

    [Fact]
    public void Clear_ResetsAllCounters()
    {
        // Arrange
        var mock = new MockMetricsCollector();
        mock.StartRequest();
        mock.RecordFirstToken();
        mock.RecordCompletion(10, 100);

        // Act
        mock.Clear();

        // Assert
        mock.StartRequestCount.ShouldBe(0);
        mock.FirstTokenCount.ShouldBe(0);
        mock.CompletionCount.ShouldBe(0);
        mock.GetLatestMetrics().ShouldBeNull();
        mock.GetAllMetrics().Count.ShouldBe(0);
    }

    [Fact]
    public void GetAllMetrics_ReturnsCompletedRequests()
    {
        // Arrange
        var mock = new MockMetricsCollector();

        // Act
        mock.StartRequest();
        mock.RecordCompletion(10, 100);
        mock.StartRequest();
        mock.RecordCompletion(20, 200);

        // Assert
        var history = mock.GetAllMetrics();
        history.Count.ShouldBe(2);
    }

    [Fact]
    public void SimulateFastResponse_DefaultsToTrue()
    {
        // Arrange & Act
        var mock = new MockMetricsCollector();

        // Assert
        mock.SimulateFastResponse.ShouldBeTrue();
    }

    [Fact]
    public void GetMetrics_ReturnsCurrent()
    {
        // Arrange
        var mock = new MockMetricsCollector();
        mock.StartRequest();

        // Act
        var metrics = mock.GetMetrics("any-id");

        // Assert
        // Mock returns same current metrics for any ID
        metrics.ShouldNotBeNull();
    }

    [Fact]
    public void RecordFirstToken_OnlyUpdatesOnce()
    {
        // Arrange
        var mock = new MockMetricsCollector
        {
            FixedTimeToFirstToken = TimeSpan.FromMilliseconds(500)
        };
        mock.StartRequest();

        // Act
        mock.RecordFirstToken();
        var firstTime = mock.GetLatestMetrics()?.FirstTokenTime;
        mock.FixedTimeToFirstToken = TimeSpan.FromMilliseconds(1000);
        mock.RecordFirstToken();

        // Assert
        mock.GetLatestMetrics()?.FirstTokenTime.ShouldBe(firstTime);
    }
}
