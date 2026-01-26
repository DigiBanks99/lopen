using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class MockWelcomeHeaderRendererTests
{
    [Fact]
    public void WasCalled_FalseInitially()
    {
        var renderer = new MockWelcomeHeaderRenderer();

        renderer.WasCalled.ShouldBeFalse();
        renderer.RenderCalls.Count.ShouldBe(0);
    }

    [Fact]
    public void RenderWelcomeHeader_RecordsCall()
    {
        var renderer = new MockWelcomeHeaderRenderer();
        var context = new WelcomeHeaderContext { Version = "1.0.0" };

        renderer.RenderWelcomeHeader(context);

        renderer.WasCalled.ShouldBeTrue();
        renderer.RenderCalls.Count.ShouldBe(1);
        renderer.RenderCalls[0].Version.ShouldBe("1.0.0");
    }

    [Fact]
    public void LastContext_ReturnsLastRenderedContext()
    {
        var renderer = new MockWelcomeHeaderRenderer();
        var context1 = new WelcomeHeaderContext { Version = "1.0.0" };
        var context2 = new WelcomeHeaderContext { Version = "2.0.0" };

        renderer.RenderWelcomeHeader(context1);
        renderer.RenderWelcomeHeader(context2);

        renderer.LastContext!.Version.ShouldBe("2.0.0");
    }

    [Fact]
    public void LastContext_NullWhenNoCalls()
    {
        var renderer = new MockWelcomeHeaderRenderer();

        renderer.LastContext.ShouldBeNull();
    }

    [Fact]
    public void Reset_ClearsAllCalls()
    {
        var renderer = new MockWelcomeHeaderRenderer();
        renderer.RenderWelcomeHeader(new WelcomeHeaderContext { Version = "1.0.0" });
        renderer.RenderWelcomeHeader(new WelcomeHeaderContext { Version = "2.0.0" });

        renderer.Reset();

        renderer.WasCalled.ShouldBeFalse();
        renderer.RenderCalls.Count.ShouldBe(0);
        renderer.LastContext.ShouldBeNull();
    }
}
