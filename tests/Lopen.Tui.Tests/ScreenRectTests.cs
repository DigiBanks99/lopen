using Lopen.Tui;

namespace Lopen.Tui.Tests;

public class ScreenRectTests
{
    [Fact]
    public void Inflate_Negative_Shrinks()
    {
        var rect = new ScreenRect(5, 5, 20, 10);

        var inner = rect.Inflate(-1, -1);

        Assert.Equal(new ScreenRect(6, 6, 18, 8), inner);
    }

    [Fact]
    public void Inflate_Positive_Grows()
    {
        var rect = new ScreenRect(5, 5, 20, 10);

        var outer = rect.Inflate(2, 1);

        Assert.Equal(new ScreenRect(3, 4, 24, 12), outer);
    }

    [Fact]
    public void Inflate_Zero_NoChange()
    {
        var rect = new ScreenRect(0, 0, 80, 24);

        var same = rect.Inflate(0, 0);

        Assert.Equal(rect, same);
    }

    [Fact]
    public void Properties_AreAccessible()
    {
        var rect = new ScreenRect(10, 20, 30, 40);

        Assert.Equal(10, rect.X);
        Assert.Equal(20, rect.Y);
        Assert.Equal(30, rect.Width);
        Assert.Equal(40, rect.Height);
    }
}
