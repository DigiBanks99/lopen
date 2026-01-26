using Shouldly;
using Lopen.Core.Testing;

namespace Lopen.Core.Tests.Testing;

public class TestContextTests
{
    [Fact]
    public void TestContext_HasDefaults()
    {
        var context = new TestContext();
        
        context.Model.ShouldBe("gpt-5-mini");
        context.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
        context.Verbose.ShouldBeFalse();
        context.LopenPath.ShouldBe("lopen");
    }
    
    [Fact]
    public void TestContext_CanOverrideDefaults()
    {
        var context = new TestContext
        {
            Model = "gpt-5",
            Timeout = TimeSpan.FromSeconds(60),
            Verbose = true,
            LopenPath = "/usr/bin/lopen"
        };
        
        context.Model.ShouldBe("gpt-5");
        context.Timeout.ShouldBe(TimeSpan.FromSeconds(60));
        context.Verbose.ShouldBeTrue();
        context.LopenPath.ShouldBe("/usr/bin/lopen");
    }
}
