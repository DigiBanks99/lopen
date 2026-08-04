using Lopen.Tui;

namespace Lopen.Tui.Tests;

public class RelativeTimeFormatterTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void JustNow_LessThanOneMinute()
    {
        DateTimeOffset timestamp = Now.AddSeconds(-30);
        Assert.Equal("just now", RelativeTimeFormatter.FormatRelativeTime(timestamp, Now));
    }

    [Fact]
    public void JustNow_ZeroSeconds()
    {
        Assert.Equal("just now", RelativeTimeFormatter.FormatRelativeTime(Now, Now));
    }

    [Theory]
    [InlineData(1, "1m ago")]
    [InlineData(30, "30m ago")]
    [InlineData(59, "59m ago")]
    public void Minutes_Formatting(int minutes, string expected)
    {
        DateTimeOffset timestamp = Now.AddMinutes(-minutes);
        Assert.Equal(expected, RelativeTimeFormatter.FormatRelativeTime(timestamp, Now));
    }

    [Theory]
    [InlineData(1, "1h ago")]
    [InlineData(12, "12h ago")]
    [InlineData(23, "23h ago")]
    public void Hours_Formatting(int hours, string expected)
    {
        DateTimeOffset timestamp = Now.AddHours(-hours);
        Assert.Equal(expected, RelativeTimeFormatter.FormatRelativeTime(timestamp, Now));
    }

    [Theory]
    [InlineData(1, "1d ago")]
    [InlineData(6, "6d ago")]
    public void Days_Formatting(int days, string expected)
    {
        DateTimeOffset timestamp = Now.AddDays(-days);
        Assert.Equal(expected, RelativeTimeFormatter.FormatRelativeTime(timestamp, Now));
    }

    [Theory]
    [InlineData(7, "1w ago")]
    [InlineData(28, "4w ago")]
    public void Weeks_Formatting(int days, string expected)
    {
        DateTimeOffset timestamp = Now.AddDays(-days);
        Assert.Equal(expected, RelativeTimeFormatter.FormatRelativeTime(timestamp, Now));
    }

    [Theory]
    [InlineData(30, "1mo ago")]
    [InlineData(180, "6mo ago")]
    public void Months_Formatting(int days, string expected)
    {
        DateTimeOffset timestamp = Now.AddDays(-days);
        Assert.Equal(expected, RelativeTimeFormatter.FormatRelativeTime(timestamp, Now));
    }

    [Fact]
    public void FutureTimestamp_ReturnsJustNow()
    {
        DateTimeOffset future = Now.AddMinutes(5);
        Assert.Equal("just now", RelativeTimeFormatter.FormatRelativeTime(future, Now));
    }
}
