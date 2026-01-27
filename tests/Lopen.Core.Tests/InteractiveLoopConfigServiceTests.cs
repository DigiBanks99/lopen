using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class InteractiveLoopConfigServiceTests
{
    [Fact]
    public void MockService_TracksWasCalled()
    {
        var service = new MockInteractiveLoopConfigService();
        var config = new LoopConfig();
        
        service.WasCalled.ShouldBeFalse();
        
        service.PromptForConfiguration(config);
        
        service.WasCalled.ShouldBeTrue();
    }
    
    [Fact]
    public void MockService_TracksConfigPassed()
    {
        var service = new MockInteractiveLoopConfigService();
        var config = new LoopConfig { Model = "test-model" };
        
        service.PromptForConfiguration(config);
        
        service.LastConfigPassed.ShouldNotBeNull();
        service.LastConfigPassed.Model.ShouldBe("test-model");
    }
    
    [Fact]
    public void MockService_ReturnsSetResult()
    {
        var service = new MockInteractiveLoopConfigService();
        var expectedConfig = new LoopConfig { Model = "new-model" };
        service.SetNextResult(new InteractiveLoopConfigResult
        {
            Cancelled = false,
            Config = expectedConfig
        });
        
        var result = service.PromptForConfiguration(new LoopConfig());
        
        result.Cancelled.ShouldBeFalse();
        result.Config.ShouldNotBeNull();
        result.Config.Model.ShouldBe("new-model");
    }
    
    [Fact]
    public void MockService_ReturnsCancelledByDefault()
    {
        var service = new MockInteractiveLoopConfigService();
        
        var result = service.PromptForConfiguration(new LoopConfig());
        
        result.Cancelled.ShouldBeTrue();
        result.Config.ShouldBeNull();
    }
    
    [Fact]
    public void InteractiveLoopConfigResult_DefaultValues()
    {
        var result = new InteractiveLoopConfigResult();
        
        result.Cancelled.ShouldBeFalse();
        result.Config.ShouldBeNull();
    }
    
    [Fact]
    public void InteractiveLoopConfigResult_WithValues()
    {
        var config = new LoopConfig { Model = "test" };
        var result = new InteractiveLoopConfigResult
        {
            Cancelled = true,
            Config = config
        };
        
        result.Cancelled.ShouldBeTrue();
        result.Config.ShouldBe(config);
    }
    
    [Fact]
    public void SpectreService_ThrowsOnNullConsole()
    {
        Should.Throw<ArgumentNullException>(() => new SpectreInteractiveLoopConfigService(null!));
    }
}
